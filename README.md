# EMMA Video Test Plugin

Deterministic plugin for validating host and UI video integration.

## Run

```bash
dotnet run --project EMMA.VideoTest.csproj
```

Default port is 5005. Override with:

```bash
dotnet run --project EMMA.VideoTest.csproj -- --port 6001
```

or

```bash
EMMA_TEST_PLUGIN_PORT=6001 dotnet run --project EMMA.VideoTest.csproj
```

## Build and pack

From repo root:

```bash
./scripts/build-pack-plugin.sh ./EMMA.VideoTest.plugin.json
```

WASM package variant:

```bash
TARGETS="wasm" ./scripts/build-pack-plugin.sh ./EMMA.VideoTest.plugin.json
```

ASP.NET plugin package variant (example Linux x64):

```bash
TARGETS="linux-x64" ./scripts/build-pack-plugin-aspnet.sh ./EMMA.VideoTest.plugin.json
```

## Test scenario matrix

Search returns deterministic video fixtures.

- `video-hls-single`: one stream with one playlist URI.
- `video-hls-multi`: multiple quality streams for selection UI.
- `video-segment-basic`: stream supports deterministic segment sequence `0..4`.
- `video-empty-streams`: returns an empty stream list.

Segment behavior for `video-segment-basic` + `segment-main`:

- `sequence` 0..4: returns payload and `contentType=video/mp2t`.
- any other sequence: returns empty response (not found path).

## Quick validation checklist

1. Open plugin `emma.video.test` in host UI.
2. Search with empty query and confirm all four fixtures are visible.
3. Open `video-hls-multi` and confirm multiple streams appear.
4. Open `video-segment-basic`, pick `segment-main`, fetch sequence `0` and confirm non-zero payload.
5. Fetch sequence `99` for `video-segment-basic` and confirm miss behavior is surfaced.
6. Open `video-empty-streams` and confirm stream list is empty.

## Notes

- Plugin behavior is synthetic and does not depend on external providers.
- Page/chapter endpoints are intentionally stubbed in this project because this plugin focuses on video path validation.
