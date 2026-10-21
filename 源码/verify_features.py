"""Independent development fixtures and verification; never shipped in the EXE.

Usage: python verify_features.py fixtures|verify <directory>
Uses cryptography, pypdf, reportlab, PyMuPDF and an optional OpenSSL executable.
All generated files stay in the supplied test directory.
"""
import datetime as dt
import hashlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True


def fixtures(root):
    from cryptography import x509
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa
    from cryptography.hazmat.primitives.serialization import pkcs12
    from cryptography.x509.oid import NameOID
    from reportlab.pdfgen import canvas
    from pypdf import PdfReader, PdfWriter
    import fitz

    root.mkdir(parents=True, exist_ok=True)
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'Mini PDF TEST ONLY')])
    now = dt.datetime.now(dt.timezone.utc)
    for label, start, end in [('signing', now-dt.timedelta(days=1), now+dt.timedelta(days=365)),
                              ('expired', now-dt.timedelta(days=365), now-dt.timedelta(days=1))]:
        cert = (x509.CertificateBuilder().subject_name(name).issuer_name(name)
                .public_key(key.public_key()).serial_number(x509.random_serial_number())
                .not_valid_before(start).not_valid_after(end)
                .add_extension(x509.KeyUsage(True, True, False, False, False, False, False, False, False), True)
                .sign(key, hashes.SHA256()))
        encryption = (serialization.PrivateFormat.PKCS12.encryption_builder()
                      .kdf_rounds(2000)
                      .key_cert_algorithm(pkcs12.PBES.PBESv1SHA1And3KeyTripleDESCBC)
                      .hmac_hash(hashes.SHA1()).build(b'minipdf-test'))
        (root / (label+'.pfx')).write_bytes(pkcs12.serialize_key_and_certificates(b'Test only', key, cert, None, encryption))
        (root / (label+'.cer')).write_bytes(cert.public_bytes(serialization.Encoding.DER))
    stream = io.BytesIO()
    c = canvas.Canvas(stream, pagesize=(400, 600))
    for index in range(8):
        if index == 2:
            c.setPageSize((600, 400))
        else:
            c.setPageSize((400, 600))
        c.setFillColorRGB(.9, .1, .2)
        c.rect(0, 0, 90, 140, stroke=0, fill=1)
        c.setFillColorRGB(.1, .6, .9)
        c.rect(120, 180, 80, 80, stroke=0, fill=1)
        c.setFillColorRGB(0, 0, 0)
        c.setFont('Helvetica', 20)
        c.drawString(55, 300, f'FEATURE PAGE {index+1}')
        c.showPage()
    c.save()
    (root / 'features.pdf').write_bytes(stream.getvalue())
    stream = io.BytesIO()
    c = canvas.Canvas(stream, pagesize=(400, 600))
    c.drawString(45, 500, 'FORM FIELD MUST SURVIVE SIGNING')
    c.acroForm.textfield(name='customer', tooltip='Customer', x=45, y=400, width=230, height=30, value='PRESERVED VALUE')
    c.showPage()
    c.save()
    (root / 'form.pdf').write_bytes(stream.getvalue())
    with fitz.open(root / 'features.pdf') as pdf:
        pdf.save(root / 'object-streams.pdf', use_objstms=1, garbage=4)
    writer = PdfWriter()
    writer.append(PdfReader(root / 'features.pdf'))
    writer.pages[0].rotate(90)
    writer.pages[1].rotate(180)
    writer.pages[2].rotate(270)
    with (root / 'rotated.pdf').open('wb') as out:
        writer.write(out)


def verify(root):
    from pypdf import PdfReader
    from cryptography.hazmat.primitives.serialization import pkcs7
    import fitz
    import shutil

    checks = []

    def check(ok, message):
        if not ok:
            raise AssertionError(message)
        checks.append(message)

    for name in ['signed.pdf', 'signed-form.pdf', 'signed-streams.pdf']:
        data = (root / name).read_bytes()
        reader = PdfReader(io.BytesIO(data), strict=True)
        fields = reader.get_fields()
        sigs = [v['/V'].get_object() for v in fields.values() if v.get('/FT') == '/Sig']
        check(len(sigs) == 1, name+': exactly one AcroForm signature')
        sig = sigs[0]
        ranges = list(sig['/ByteRange'])
        check(ranges[0] == 0 and ranges[2]+ranges[3] == len(data), name+': full-file coverage')
        contents = sig['/Contents'].original_bytes
        n = contents[1] & 127 if contents[1] & 128 else 0
        total = 2+n+int.from_bytes(contents[2:2+n], 'big') if n else 2+contents[1]
        check(not any(contents[total:]), name+': signature reservation contains only zero padding')
        contents = contents[:total]
        check(sig['/SubFilter'] == '/adbe.pkcs7.detached', name+': standard detached CMS')
        check(len(pkcs7.load_der_pkcs7_certificates(contents)) == 1, name+': embedded certificate readable independently')
        signed = data[:ranges[1]]+data[ranges[2]:ranges[2]+ranges[3]]
        check(data[ranges[1]] == 60 and data[ranges[2]-1] == 62, name+': digest excludes only hex contents')
        openssl = Path(shutil.which('openssl') or 'C:/Program Files/Git/usr/bin/openssl.exe')
        if openssl.is_file():
            cms_path = root / (name+'.cms')
            bytes_path = root / (name+'.bytes')
            cms_path.write_bytes(contents)
            bytes_path.write_bytes(signed)
            env = dict(os.environ, TEMP=str(root), TMP=str(root))
            result = subprocess.run([str(openssl), 'cms', '-verify', '-binary', '-noverify', '-inform', 'DER',
                                     '-in', str(cms_path), '-content', str(bytes_path), '-out', os.devnull],
                                    capture_output=True, env=env, timeout=30)
            check(result.returncode == 0, name+': OpenSSL verifies detached SHA-256 signature: '+result.stderr.decode(errors='replace').strip())
        with fitz.open(root / name) as pdf:
            check(pdf[0].get_pixmap().width > 100, name+': independent renderer opens signed document')
    form = PdfReader(root / 'signed-form.pdf').get_fields()
    check(form['customer']['/V'] == 'PRESERVED VALUE', 'Existing AcroForm fields survive signing')
    check((root / 'signed.pdf').read_bytes() == (root / 'signed-copy.pdf').read_bytes(), 'Saving signed copy preserves every byte')
    watermark = PdfReader(root / 'watermarked.pdf')
    check(len(watermark.pages) == 8, 'Watermark preserves all pages')
    for index, page in enumerate(watermark.pages):
        check(f'TEST {index+1} / 8' in page.extract_text(), f'Watermark page {index+1}: correct page counters')
    removed = PdfReader(root / 'watermarks-removed.pdf')
    check(all('TEST ' not in p.extract_text() for p in removed.pages), 'Removing tagged watermarks preserves original text')
    merged = PdfReader(root / 'combined.pdf')
    check(len(merged.pages) == 8, 'Combined page count independently correct')
    expected = [4, 5, 1, 2, 3, 6, 7, 8]
    for index, original in enumerate(expected):
        check(f'FEATURE PAGE {original}' in merged.pages[index].extract_text(), f'Combined page {index+1}: matches reordered input {original}')
    check(int(merged.pages[0].get('/Rotate', 0)) % 360 == 90, 'Per-page merge rotation retained')
    for name in ['watermarked.pdf', 'watermarks-removed.pdf', 'image-watermark.pdf', 'visible-signature.pdf', 'combined.pdf']:
        with fitz.open(root / name) as pdf:
            pix = pdf[0].get_pixmap(matrix=fitz.Matrix(1, 1), alpha=False)
            pix.save(root / (name[:-4]+'-independent.png'))
            check(pix.width > 100, name+': independent rendering works')
    (root / 'feature-independent-checks.json').write_text(json.dumps({'passed':len(checks),'checks':checks}, indent=2), encoding='utf8')
    print(f'Independent feature checks: {len(checks)} passed')


if __name__ == '__main__':
    action, directory = sys.argv[1:]
    {'fixtures': fixtures, 'verify': verify}[action](Path(directory))
