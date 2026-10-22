#!/usr/bin/env bash
# Prints every asset-pipeline tool with its version, and fails if any is missing.
#   bash asset_work/scripts/check_toolchain.sh
source "${GAME_TOOLCHAIN:-$HOME/game-toolchain}/env.sh" >/dev/null 2>&1
missing=0
row() {  # label command... (first line of output is shown)
  local label="$1"; shift
  if command -v "$1" >/dev/null 2>&1; then
    local v; v=$(timeout 60 "$@" 2>&1 | grep -v "WARNING\|Fontconfig\|^$\|^-* *$\|dbind" | head -1 | cut -c1-70)
    printf "  %-16s %s\n" "$label" "${v:-ok}"
  else
    printf "  %-16s MISSING\n" "$label"; missing=1
  fi
}
py() {  # label module
  local v; v=$(python3 -c "import $2 as m; print(getattr(m,'__version__',''))" 2>/dev/null) && printf "  %-16s %s\n" "$1" "${v:-ok}" || { printf "  %-16s MISSING\n" "$1"; missing=1; }
}
echo "3D";      row blender blender --version; row assimp bash -c "assimp version | grep -i 'Version' | head -1"; row gltf-transform gltf-transform --version
row gltf-pipeline gltf-pipeline --version; row gltfpack gltfpack -h; row obj2gltf obj2gltf --help; row fbx2gltf fbx2gltf --version
row meshlab meshlab --version; py trimesh trimesh; py pymeshlab pymeshlab; py xatlas xatlas; py pygltflib pygltflib; py shapely shapely
echo "Textures / images"; row magick magick -version; row inkscape inkscape --version; row krita bash -c "xvfb-run -a krita --version 2>&1 | grep '^krita' | head -1"
row toktx toktx --version; row basisu bash -c "basisu -version 2>&1 | head -1"; row ktx ktx --version; row oxipng oxipng --version; row pngquant pngquant --version; row optipng optipng --version
row zopflipng zopflipng --help; row potrace potrace --version; row cwebp cwebp -version; row gifsicle gifsicle --version
py pillow PIL; py numpy numpy; py scipy scipy; py scikit-image skimage; py opencv cv2; py opensimplex opensimplex; py noise noise; py cairosvg cairosvg; py imageio imageio
echo "Audio / music"; row fluidsynth fluidsynth --version; row sox sox --version; row ffmpeg ffmpeg -version; row oggenc oggenc --version
row opusenc opusenc --version; row lame lame --version; row flac flac --version; row rubberband rubberband --help; row espeak-ng espeak-ng --version
row abc2midi abc2midi -version; row lmms bash -c "xvfb-run -a lmms --version 2>&1 | grep '^LMMS' | head -1"
py midiutil midiutil; py mido mido; py pretty_midi pretty_midi; py music21 music21; py librosa librosa; py pedalboard pedalboard
py soundfile soundfile; py pyloudnorm pyloudnorm; py pydub pydub; py pyfluidsynth fluidsynth
echo "Build / misc"; row unity unity --version; row node node --version; row npm npm --version; row gcc gcc --version; row g++ g++ --version; row make make --version; row cmake cmake --version; row ninja ninja --version
row 7z 7z i; row xvfb-run xvfb-run --help; row jq jq --version
[ -x "${UNITY_EDITOR:-/nonexistent}" ] && echo "  unity-editor     $UNITY_EDITOR" || { echo "  unity-editor     MISSING"; missing=1; }
exit $missing
