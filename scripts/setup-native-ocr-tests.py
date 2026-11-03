#!/usr/bin/env python3
"""Prepare real Linux OCR fixtures/tools for source tests only. Never a Windows release bundle."""
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import pypdfium2_raw
from PIL import Image, ImageDraw, ImageFont

root = Path(os.environ.get('WI_NATIVE_TEST_ROOT', '/tmp/wi-native-ocr-tests')).resolve()
runtime = root / 'runtime'
fixtures = root / 'fixtures'
for path in (runtime, fixtures, runtime / 'tessdata'):
    path.mkdir(parents=True, exist_ok=True)
# Source-test binaries retain their vendor/platform format; the .exe/.dll names
# intentionally exercise the application's normal native component resolver.
tesseract = os.environ.get('WI_TESSERACT_SOURCE') or shutil.which('tesseract')
if not tesseract:
    raise RuntimeError('Install distro tesseract-ocr and English traineddata for native source tests')
shutil.copy2(tesseract, runtime / 'tesseract.exe')
trained = Path(os.environ['WI_TESSDATA_SOURCE']) if os.environ.get('WI_TESSDATA_SOURCE') else next((p for parent in (Path('/usr/share/tesseract-ocr'), Path('/usr/share/tessdata'))
                if parent.exists() for p in parent.rglob('eng.traineddata')), None)
if not trained:
    raise RuntimeError('English OCR traineddata is missing')
shutil.copy2(trained, runtime / 'tessdata/eng.traineddata')
pdfium = Path(pypdfium2_raw.__file__).parent / ('pdfium.dll' if os.name == 'nt' else 'libpdfium.so')
shutil.copy2(pdfium, runtime / 'pdfium.dll')
if os.name == 'nt':
    for library in Path(tesseract).parent.glob('*.dll'):
        shutil.copy2(library, runtime / library.name)
rclone = os.environ.get('WI_RCLONE_SOURCE')
if rclone:
    shutil.copy2(rclone, runtime / 'rclone.exe')
entries = [f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.relative_to(runtime).as_posix()}'
           for p in sorted(runtime.rglob('*')) if p.is_file() and p.name != 'SHA256SUMS.txt']
(runtime / 'SHA256SUMS.txt').write_text('\n'.join(entries) + '\n')
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf' if os.name == 'nt' else '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 48)
image = Image.new('RGB', (1300, 240), 'white')
ImageDraw.Draw(image).text((35, 70), 'SYNTHETIC RECOVERY 427', fill='black', font=font)
for extension in ('png', 'jpeg', 'bmp', 'tiff'):
    image.save(fixtures / ('synthetic.' + extension))
(root / 'versions.txt').write_text(subprocess.check_output([tesseract, '--version'], text=True).splitlines()[0]
                                  + '\npypdfium2 5.14.0; Pillow 13.0.0\n')
print(f'Real native source-test runtime and synthetic fixtures prepared: {root}')
