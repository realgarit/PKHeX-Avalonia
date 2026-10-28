#!/usr/bin/env bash
set -euo pipefail

version=${1:?Missing version}
revision=${2:?Missing source commit}
release_date=${3:?Missing release date}
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
[[ "$revision" =~ ^[0-9a-f]{40}$ ]]
[[ "$release_date" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]
app_id=io.github.realgarit.PKHeX-Avalonia
test "${FLATPAK_ID:?}" = "$app_id"
test -x /app/lib/pkhex-avalonia/PKHeX.Avalonia

install -d /app/bin /app/share/applications /app/share/metainfo \
  /app/share/icons/hicolor/scalable/apps /app/share/licenses/"$app_id"
cat > /app/bin/PKHeX.Avalonia << 'EOF'
#!/bin/sh
exec /app/lib/pkhex-avalonia/PKHeX.Avalonia "$@"
EOF
chmod +x /app/bin/PKHeX.Avalonia
sed "s/io.pkhex.avalonia/$app_id/g" packaging/linux/io.pkhex.avalonia.desktop \
  > /app/share/applications/"$app_id".desktop
sed -e "s/io.pkhex.avalonia/$app_id/g" \
    -e "s/@SOURCE_REVISION@/$revision/g" \
    -e "s/@VERSION@/$version/g" -e "s/@RELEASE_DATE@/$release_date/g" \
  packaging/linux/io.pkhex.avalonia.metainfo.xml \
  > /app/share/metainfo/"$app_id".metainfo.xml
install -m644 packaging/linux/io.pkhex.avalonia.svg \
  /app/share/icons/hicolor/scalable/apps/"$app_id".svg
install -m644 LICENSE /app/share/licenses/"$app_id"/LICENSE
install -m644 PKHeX.Avalonia/Assets/ThirdParty/pokesprite-LICENSE.txt \
  /app/share/licenses/"$app_id"/pokesprite-LICENSE.txt
install -m644 PKHeX.Avalonia/Assets/ThirdParty/za-key-item-sources.md \
  PKHeX.Avalonia/Assets/ThirdParty/pokesprite-sources.md \
  PKHeX.AutoMod/VENDORED.md /app/share/licenses/"$app_id"/
desktop-file-validate /app/share/applications/"$app_id".desktop
appstreamcli validate --no-net /app/share/metainfo/"$app_id".metainfo.xml
