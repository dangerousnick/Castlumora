"""Retain NuGet package license metadata and any bundled license/notice files."""
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET
assets = json.loads(Path(sys.argv[1]).read_text())
output = Path(sys.argv[2])
output.mkdir(parents=True, exist_ok=True)
folders = [Path(path) for path in assets['packageFolders']]
lines = ['Castlumora server — NuGet dependency provenance and license metadata',
         'The GPL source LICENSE and upstream CONTRIBUTORS remain separate. Each dependency retains its own license.',
         'Runtime framework license and notices accompany the self-contained server where provided by the runtime pack.', '']
for key, item in sorted(assets['libraries'].items()):
    if item['type'] != 'package':
        continue
    package = next((folder / item['path'] for folder in folders if (folder / item['path']).exists()), None)
    if package is None:
        raise SystemExit('Package license metadata missing: ' + key)
    spec = next(package.glob('*.nuspec'))
    tree = ET.parse(spec)
    metadata = next(element for element in tree.getroot() if element.tag.endswith('metadata'))
    values = {element.tag.split('}')[-1]: element for element in metadata}
    lines.append(key + ' — https://www.nuget.org/packages/' + key)
    for field in ['authors', 'copyright', 'license', 'licenseUrl', 'projectUrl']:
        if field in values:
            lines.append(field + ': ' + (values[field].text or ''))
    destination = output / key.replace('/', '-')
    licenses = [file for file in package.rglob('*') if file.is_file() and
                file.name.lower().startswith(('license', 'notice', 'thirdpartynotice', 'copying', 'copyright')) and file.stat().st_size < 4 * 1024 * 1024]
    declared = values.get('license')
    if declared is not None and declared.attrib.get('type') == 'file':
        path = package / (declared.text or '')
        if not path.is_file():
            raise SystemExit('Declared license not found: ' + key)
        licenses.append(path)
    for index, file in enumerate(dict.fromkeys(licenses)):
        destination.mkdir(exist_ok=True)
        shutil.copyfile(file, destination / (str(index) + '-' + file.name))
    lines.append('')
(output / 'NUGET_LICENSES.txt').write_text('\n'.join(lines))
