# UI layer 1 review captures

Offscreen Qt captures (light palette) for the draft PR that ports the application shell from the draft UI stack.
"Before" is `main` at 8d3f5f6, "after" is `ui/api-shell`.

Everything shown is synthetic except image 13. The map uses neutral generated tiles, not OneMap imagery. Images 01-12
use a generated, API-shaped fixture of 420 addresses with real-style street names and invented figures. Image 13 uses
the recorded Worker API responses in `tests/fixtures/worker-api`.

The offscreen platform does not paint native macOS control chrome and ignores dark appearance, so these images show
layout, hierarchy and density only. This branch holds review assets only and is not meant to be merged.
