Mini PDF 0.12.8 keeps the previous raster visible during zoom changes, then replaces it with the exact full-resolution page or tiles. Previously a new zoom size could display a blank white page while background rendering finished.

Preview reuse stays inside the existing 64 MiB cache. Editing or reloading clears old pixels. The normal ready-page paint path bypasses preview scaling. Raster worker connection cancellation also completes pending asynchronous operations without closing their wait handles prematurely.

Validation: 53 zoom assertions and 30 raster/performance assertions passed locally. These include immediate intermediate frames, rapid repeated zoom, tiled magnification, original-resolution pixel equality at four rotations, edited-source invalidation, worker restart and shutdown.
