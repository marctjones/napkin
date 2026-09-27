/*
 * napkin_mlx.h — the C ABI of napkin's MLX bridge, version 1.
 *
 * docs/design/mlx-runtime.md §1.4 is the specification; this header is the whole surface. The
 * bridge (native/NapkinMlx, Swift over mlx-swift-lm) exports exactly these eight functions and
 * nothing else. napkin calls them through P/Invoke (Napkin.Assistant.Mlx, slice B).
 *
 * Rules:
 *   - Every string is UTF-8 and NUL-terminated.
 *   - Every pointer the bridge RETURNS is owned by the bridge and freed by the matching _free
 *     (napkin_mlx_result_free for a result, napkin_mlx_string_free for an *error sentence).
 *   - Every pointer napkin PASSES is read during the call and never kept — except `cancel`, which
 *     the bridge reads (atomically) until the call returns.
 *   - No callbacks. Cancellation is a flag napkin owns (non-zero = cancel) and the bridge polls.
 *   - Every function blocks the calling thread. Call from a thread-pool thread, never from a UI
 *     thread and never from a Swift cooperative thread.
 *   - `error` may be NULL. When it is not, it is set to NULL on OK, and to a sentence the caller
 *     frees with napkin_mlx_string_free on any other status.
 *
 * napkin's own code, AGPL-3.0 like the rest of napkin.
 */
#ifndef NAPKIN_MLX_H
#define NAPKIN_MLX_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct napkin_mlx_model napkin_mlx_model;          /* opaque: a loaded model */

typedef enum {
    NAPKIN_MLX_OK          = 0,
    NAPKIN_MLX_ERROR       = 1,   /* *error says what, in a sentence; nothing was produced */
    NAPKIN_MLX_CANCELLED   = 2,   /* the cancel flag was set; nothing was produced */
    NAPKIN_MLX_BUSY        = 3,   /* a generation is already running on this model */
    NAPKIN_MLX_NO_METAL    = 4,   /* no Metal device (headless, virtualized, or not Apple silicon) */
    NAPKIN_MLX_INCOMPLETE  = 5,   /* guided: max_tokens ran out before the schema was satisfied */
} napkin_mlx_status;

typedef struct {
    int32_t  has_metal;                     /* 0/1 */
    char     architecture[64];              /* Metal's device.architecture.name, or "Unknown" */
    uint64_t memory_bytes;                  /* hw.memsize */
    uint64_t recommended_working_set_bytes; /* Metal's recommendedMaxWorkingSetSize, 0 if none */
    char     mlx_swift_lm_revision[48];     /* the commit the bridge was built against */
} napkin_mlx_device_info;

typedef struct {
    const char* text;               /* the whole reply (guided: the JSON document), bridge-owned */
    int32_t  prompt_tokens;
    int32_t  generated_tokens;
    int32_t  stop_reason;           /* 0 stop token, 1 length, 2 cancelled, 3 schema complete */
    double   prompt_seconds;
    double   generation_seconds;
} napkin_mlx_result;

/* 1. */
int32_t           napkin_mlx_abi_version(void);

/* Once per process. `metallib_path` must be the mlx.metallib beside libNapkinMlx.dylib (MLX
 * 0.31 loads its Metal library only from there — as-built note in mlx-runtime.md). Probes the
 * Metal device before touching MLX: NO_METAL, never a crash. A second call after OK is a no-op. */
napkin_mlx_status napkin_mlx_init(const char* metallib_path, char** error);

/* No model needed; does not need init. */
napkin_mlx_status napkin_mlx_device(napkin_mlx_device_info* out, char** error);

/* Loads an mlx-community folder (config.json, tokenizer.json, tokenizer_config.json,
 * *.safetensors). Not interruptible inside; `cancel` is read before and after. */
napkin_mlx_status napkin_mlx_load(const char* model_dir, const int32_t* cancel,
                                  napkin_mlx_model** out, char** error);

napkin_mlx_status napkin_mlx_generate(napkin_mlx_model* model,
                                      const char* system, const char* user,
                                      const char* json_schema,       /* NULL = free text */
                                      int32_t max_tokens, float temperature, float top_p,
                                      int32_t enable_thinking,       /* 0/1, chat-template flag */
                                      const int32_t* cancel,         /* polled every token */
                                      napkin_mlx_result** out, char** error);

void              napkin_mlx_result_free(napkin_mlx_result* result);
void              napkin_mlx_string_free(char* string);
void              napkin_mlx_unload(napkin_mlx_model* model);

#ifdef __cplusplus
}
#endif

#endif /* NAPKIN_MLX_H */
