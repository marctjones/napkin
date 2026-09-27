#!/bin/zsh
# Builds napkin's MLX bridge (docs/design/mlx-runtime.md §3.2) into native/NapkinMlx/out/:
#   libNapkinMlx.dylib            the bridge (the C ABI of native/NapkinMlx/include/napkin_mlx.h)
#   mlx.metallib                  MLX's Metal kernels; MLX loads it from beside the dylib
#   libswiftCompatibilitySpan.dylib  the Swift runtime's Span back-deployment library, which the
#                                 dylib links for its macOS 14 floor (macOS 26 has its own copy)
#   napkin-mlx-spike              the spike harness (dlopens the dylib as napkin does)
# and prints the spike facts: sizes, exports, dependencies, signature, build time.
#
# Apple silicon, Xcode (26.4 or later: mlx-swift 0.31.6 needs Swift 6.3) and its Metal Toolchain
# only. Never part of `dotnet build` or tools/scripts/gate.sh.
#
#   tools/scripts/build-mlx.sh           build and copy into out/
#   tools/scripts/build-mlx.sh --test    also run the bridge's model-free Swift tests
set -o pipefail
cd "$(git rev-parse --show-toplevel)" || exit 3

run_tests=0
for argument in "$@"; do
  case "$argument" in
    --test) run_tests=1 ;;
    *) echo "usage: tools/scripts/build-mlx.sh [--test]"; exit 2 ;;
  esac
done

package=native/NapkinMlx
out=$package/out
derived=$package/.derived
log=$derived/build-mlx.log
products=$derived/Build/Products/Release

# --- refuse early, naming the fix ------------------------------------------------------------
if [ "$(uname -s)" != Darwin ] || [ "$(uname -m)" != arm64 ]; then
  echo "The MLX bridge builds on an Apple silicon Mac only (this is $(uname -s) $(uname -m))."
  exit 2
fi
if ! xcodebuild -version >/dev/null 2>&1; then
  echo "xcodebuild is not available: install Xcode 26.4 or later and run  sudo xcode-select -s /Applications/Xcode.app"
  exit 2
fi
# Read whole, not piped into `grep -q`: under pipefail a grep that stops early can fail the pipe.
metal_status=$(xcodebuild -showComponent MetalToolchain 2>/dev/null)
if [[ "$metal_status" != *"Status: installed"* ]]; then
  echo "The Metal Toolchain is missing: run  xcodebuild -downloadComponent MetalToolchain"
  exit 2
fi

mkdir -p "$derived" "$out"
echo "== $(xcodebuild -version | head -1), $(xcrun swift --version 2>/dev/null | grep -o 'Swift version [0-9.]*'), $(xcrun metal -v 2>&1 | head -1)"

# --- build -----------------------------------------------------------------------------------
started=$(date +%s)
echo "== xcodebuild: NapkinMlx and napkin-mlx-spike (Release, arm64); log: $log"
: > "$log"
for scheme in NapkinMlx napkin-mlx-spike; do
  # Xcode's generated scheme for the executable links it with -profile-generate, and the
  # instrumented spike would leave default.profraw wherever it runs.
  extra=()
  [ "$scheme" = napkin-mlx-spike ] && extra=(CLANG_COVERAGE_MAPPING=NO CLANG_ENABLE_CODE_COVERAGE=NO)
  (cd "$package" && nice -n 19 xcodebuild build -scheme "$scheme" -configuration Release \
      -destination 'platform=macOS,arch=arm64' -derivedDataPath .derived \
      -skipPackagePluginValidation "${extra[@]}") >> "$log" 2>&1
  build_status=$?
  if [ $build_status -ne 0 ]; then
    grep -E "error:" "$log" | head -20
    tail -5 "$log"
    echo "BUILD FAILED ($scheme); full log: $log"
    exit 1
  fi
done
build_seconds=$(( $(date +%s) - started ))

# --- copy what napkin ships into out/ --------------------------------------------------------
# Xcode builds a SwiftPM dynamic-library product as a framework; the Mach-O inside it is the
# dylib (its linker-made ad-hoc signature is not bound to the bundle, so the file stands alone).
# The metallib is Cmlx's resource bundle's default.metallib; MLX 0.31 finds it as mlx.metallib
# beside the binary MLX is compiled into.
framework_binary=$products/PackageFrameworks/NapkinMlx.framework/Versions/A/NapkinMlx
bundle_metallib=$products/mlx-swift_Cmlx.bundle/Contents/Resources/default.metallib
for required in "$framework_binary" "$bundle_metallib" "$products/napkin-mlx-spike"; do
  if [ ! -f "$required" ]; then
    echo "The build did not produce $required; the product layout changed — update this script."
    exit 1
  fi
done
rm -rf "$out" && mkdir -p "$out"
cp "$framework_binary" "$out/libNapkinMlx.dylib"
cp "$bundle_metallib" "$out/mlx.metallib"
cp "$products/napkin-mlx-spike" "$out/napkin-mlx-spike"
if otool -L "$out/libNapkinMlx.dylib" | grep -q '@rpath/libswiftCompatibilitySpan.dylib'; then
  span=$(find "$(dirname "$(xcrun --find swift)")/../lib" -path '*/macosx/libswiftCompatibilitySpan.dylib' | sort | tail -1)
  if [ -z "$span" ]; then
    echo "The dylib links libswiftCompatibilitySpan.dylib but the toolchain has none to copy."
    exit 1
  fi
  cp "$span" "$out/libswiftCompatibilitySpan.dylib"
fi

# --- the spike facts -------------------------------------------------------------------------
echo "== built in ${build_seconds} s"
echo "== sizes (bytes)"
for file in "$out"/*; do
  printf '  %12s  %s\n' "$(stat -f %z "$file")" "$(basename "$file")"
done
echo "== the C ABI (nm -gU)"
exports=$(nm -gU "$out/libNapkinMlx.dylib" | awk '{print $3}' | grep -E '^_napkin_mlx_' | sort)
echo "$exports" | sed 's/^/  /'
count=$(echo "$exports" | grep -c .)
if [ "$count" -ne 8 ]; then
  echo "EXPECTED the eight functions of napkin_mlx.h, found $count"
  exit 1
fi
echo "== dependencies outside the OS (otool -L)"
otool -L "$out/libNapkinMlx.dylib" | tail -n +3 | awk '{print $1}' \
  | grep -vE '^/System/Library/|^/usr/lib/' | sed 's/^/  /' || echo "  none"
echo "== signature (codesign -dvv): arm64 needs one; napkin adds none (DESIGN.md §6.6)"
codesign -dvv "$out/libNapkinMlx.dylib" 2>&1 | grep -E 'Signature|flags|Authority|TeamIdentifier' | sed 's/^/  /'
if codesign -dvv "$out/libNapkinMlx.dylib" 2>&1 | grep -q 'Authority='; then
  echo "The dylib carries a signing authority; napkin's build must not sign (DESIGN.md §6.6)."
  exit 1
fi
if grep -qE "in target 'SwiftSyntax|MLXHuggingFaceMacros" "$log"; then
  echo "== swift-syntax: compiled (it should not be; see mlx-runtime.md §1.1)"
else
  echo "== swift-syntax: not compiled"
fi

# --- the Swift tests (model-free) ------------------------------------------------------------
if [ $run_tests -eq 1 ]; then
  # build-for-testing, then the bundle run by xctest itself: `xcodebuild test` needs
  # testmanagerd over XPC, which a sandboxed agent session cannot reach (its runner times out
  # "while preparing to run tests"); xctest runs the same bundle in-process.
  echo "== xcodebuild build-for-testing: NapkinMlx-Package (Debug), then xctest"
  (cd "$package" && nice -n 19 xcodebuild build-for-testing -scheme NapkinMlx-Package \
      -destination 'platform=macOS,arch=arm64' -derivedDataPath .derived \
      -skipPackagePluginValidation) > "$derived/test-mlx.log" 2>&1
  test_status=$?
  if [ $test_status -eq 0 ]; then
    LLVM_PROFILE_FILE="$derived/test-%p.profraw" \
      xcrun xctest "$derived/Build/Products/Debug/NapkinMlxTests.xctest" >> "$derived/test-mlx.log" 2>&1
    test_status=$?
  fi
  grep -E "Executed [0-9]+ tests|\.swift:[0-9]+:.*error|failed \(" "$derived/test-mlx.log" | tail -20
  if [ $test_status -ne 0 ]; then
    echo "TESTS FAILED; full log: $derived/test-mlx.log"
    exit 1
  fi
fi

echo "== done: $out"
echo "   try:  NAPKIN_MLX_DIAGNOSTICS=1 $out/napkin-mlx-spike [--model <mlx-community folder>]"
