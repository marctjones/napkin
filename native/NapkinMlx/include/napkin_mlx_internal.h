/*
 * Not part of the ABI. Helpers the Swift bridge needs from C; `static inline`, so nothing here is
 * ever an exported symbol of libNapkinMlx.dylib (nm -gU lists only the eight of napkin_mlx.h).
 */
#ifndef NAPKIN_MLX_INTERNAL_H
#define NAPKIN_MLX_INTERNAL_H

#include <stdint.h>

/* The cancel flag napkin owns and writes from any thread (Volatile.Write in .NET). Read with
 * acquire ordering so a write on another thread is seen at the next poll and the read is never
 * hoisted out of a loop. NULL means "never cancelled". */
static inline int32_t napkin_mlx_internal_load_flag(const int32_t* flag) {
    return flag ? __atomic_load_n(flag, __ATOMIC_ACQUIRE) : 0;
}

/* What napkin does from another thread; the tests and the spike harness use it to cancel. */
static inline void napkin_mlx_internal_store_flag(int32_t* flag, int32_t value) {
    if (flag) __atomic_store_n(flag, value, __ATOMIC_RELEASE);
}

#endif /* NAPKIN_MLX_INTERNAL_H */
