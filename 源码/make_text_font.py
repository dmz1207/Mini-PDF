"""Generate the tiny, original box glyph used only by the invisible rebuilt text layer."""
from pathlib import Path
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

def main():
    builder=FontBuilder(1000,isTTF=True)
    names=['.notdef','cell'];builder.setupGlyphOrder(names)
    glyphs={}
    for name in names:
        pen=TTGlyphPen(None)
        if name=='cell':
            pen.moveTo((0,0));pen.lineTo((1000,0));pen.lineTo((1000,1000));pen.lineTo((0,1000));pen.closePath()
        glyphs[name]=pen.glyph()
    builder.setupCharacterMap({0x25a0:'cell'})
    builder.setupGlyf(glyphs)
    builder.setupHorizontalMetrics({name:(1000,0) for name in names})
    builder.setupHorizontalHeader(ascent=1000,descent=0)
    builder.setupNameTable({'familyName':'MiniPDFText','styleName':'Regular','uniqueFontIdentifier':'MiniPDFText-1','fullName':'MiniPDFText','psName':'MiniPDFText','version':'Version 1.0','copyright':'Original generated glyph; licensed under the project MIT license.'})
    builder.setupOS2(sTypoAscender=1000,sTypoDescender=0,usWinAscent=1000,usWinDescent=0)
    builder.setupPost();builder.setupMaxp();builder.font['head'].created=builder.font['head'].modified=2082844800
    path=Path(__file__).with_name('MiniPDFText.ttf');builder.save(path)
    print('Generated text geometry font:',path.stat().st_size,'bytes')

if __name__=='__main__':main()
