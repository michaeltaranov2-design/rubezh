"""Compose saved review PNGs into a labeled contact sheet and offline HTML gallery."""
import argparse
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

VIEWS=('front','side','top','three_quarter')

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('review',type=Path)
    parser.add_argument('--title',default='Rubezh — weapon review')
    args=parser.parse_args()
    root=args.review.resolve()
    meta=json.loads((root/'review.json').read_text(encoding='utf-8'))
    assets=meta['assets']
    thumb=240; gap=12; title_h=44; label_h=24; row_h=thumb+label_h+gap*2
    width=gap+len(VIEWS)*(thumb+gap)
    height=title_h+len(assets)*row_h+gap
    sheet=Image.new('RGB',(width,height),(32,36,42))
    draw=ImageDraw.Draw(sheet)
    try:font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18);small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',14)
    except OSError:font=ImageFont.load_default();small=font
    draw.text((gap,10),args.title,fill=(236,240,244),font=font)
    y=title_h
    for asset in assets:
        name=asset['assetId']
        draw.text((gap,y),name,fill=(236,196,115),font=font)
        y+=28
        for index,view in enumerate(VIEWS):
            x=gap+index*(thumb+gap)
            path=root/name/(view+'.png')
            if path.is_file():
                with Image.open(path) as image:
                    tile=ImageOps.contain(image.convert('RGB'),(thumb,thumb))
                canvas=Image.new('RGB',(thumb,thumb),(55,59,65))
                canvas.paste(tile,((thumb-tile.width)//2,(thumb-tile.height)//2))
                sheet.paste(canvas,(x,y))
            else:
                draw.rectangle((x,y,x+thumb,y+thumb),fill=(70,35,35))
                draw.text((x+8,y+8),'MISSING '+view,fill=(255,210,210),font=small)
            draw.text((x,y+thumb+3),view.replace('_',' '),fill=(202,210,218),font=small)
        y+=thumb+label_h+gap
    out=root/'preview-sheet.png'
    sheet.save(out,optimize=True)
    cards=[]
    for asset in assets:
        name=asset['assetId']
        images=''.join(f'<figure><img src="{html.escape(name)}/{view}.png"><figcaption>{html.escape(view.replace("_"," "))}</figcaption></figure>' for view in VIEWS)
        cards.append(f'<article><h2>{html.escape(name)}</h2><div class="views">{images}</div></article>')
    page='<!doctype html><meta charset="utf-8"><title>'+html.escape(args.title)+'</title><style>body{font:16px Segoe UI,Arial;background:#20242a;color:#e8edf2;margin:28px}h1{font-size:24px}h2{font-size:18px;color:#efc473;margin:22px 0 8px}.views{display:grid;grid-template-columns:repeat(4,minmax(180px,1fr));gap:12px}figure{margin:0;background:#373b41;padding:6px}img{width:100%;display:block}figcaption{padding:5px;color:#cbd3db;font-size:13px}article{max-width:1200px;margin:auto}</style><h1>'+html.escape(args.title)+'</h1>'+''.join(cards)
    (root/'gallery.html').write_text(page,encoding='utf-8')
    print(str(out))

if __name__=='__main__':main()
