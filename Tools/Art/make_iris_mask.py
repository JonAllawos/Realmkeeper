"""Phase 20A: derives the iris mask from the CC0 Quaternius eye texture (Universal Base Characters).

Output: an RGBA PNG whose RGB is the original eye texture and whose alpha is how much of each pixel is iris
(saturated, warm, inside the iris disc; the pupil, highlight, whites and skin are 0). The game recolours the iris
from it at run time (Realmkeeper.Exploration.Presentation.EyeColours). Standard library only.

Usage: python -I make_iris_mask.py <T_Eye_Brown.png> <T_Eye_IrisMask.png>
"""
import math, struct, sys, zlib

def readpng(p):
    d=open(p,'rb').read(); pos=8; idat=b''; w=h=0
    while pos<len(d):
        n=struct.unpack('>I',d[pos:pos+4])[0]; t=d[pos+4:pos+8]; c=d[pos+8:pos+8+n]; pos+=12+n
        if t==b'IHDR': w,h,bd,ct=struct.unpack('>IIBB',c[:10])
        elif t==b'IDAT': idat+=c
    raw=zlib.decompress(idat); bpp=4; stride=w*bpp; out=[]; prev=bytearray(stride); i=0
    for y in range(h):
        f=raw[i]; i+=1; line=bytearray(raw[i:i+stride]); i+=stride
        for x in range(stride):
            a=line[x-bpp] if x>=bpp else 0; b=prev[x]; c=prev[x-bpp] if x>=bpp else 0
            if f==1: line[x]=(line[x]+a)&255
            elif f==2: line[x]=(line[x]+b)&255
            elif f==3: line[x]=(line[x]+(a+b)//2)&255
            elif f==4:
                pp=a+b-c; pa=abs(pp-a); pb=abs(pp-b); pc=abs(pp-c)
                pr=a if pa<=pb and pa<=pc else (b if pb<=pc else c); line[x]=(line[x]+pr)&255
        out.append(line); prev=line
    return w,h,out

src, dst = sys.argv[1], sys.argv[2]
w,h,px = readpng(src)
def sv(r,g,b):
    mx=max(r,g,b); mn=min(r,g,b)
    return (0 if mx==0 else (mx-mn)/mx), mx/255
# first pass: candidate iris pixels (saturated, darker, warm), near the texture centre
cand=[]
for y in range(h):
    for x in range(w):
        r,g,b=px[y][x*4:x*4+3]; s,v=sv(r,g,b)
        if s>0.45 and v<0.62 and r>=g>=b and math.hypot(x-128,y-128)<40: cand.append((x,y))
cx=sum(p[0] for p in cand)/len(cand); cy=sum(p[1] for p in cand)/len(cand)
rad=sorted(math.hypot(x-cx,y-cy) for x,y in cand)[int(len(cand)*0.97)]
print('centre %.1f %.1f radius %.1f from %d pixels'%(cx,cy,rad,len(cand)))
rows=[]
for y in range(h):
    line=bytearray([0])
    for x in range(w):
        r,g,b,a=px[y][x*4:x*4+4]; d=math.hypot(x-cx,y-cy)
        s,v=sv(r,g,b)
        ring=max(0.0,min(1.0,(rad+1.5-d)/2.0))          # soft outer edge of the iris disc
        colour=max(0.0,min(1.0,(s-0.30)/0.25))           # pupil (grey/black) and highlight (white) stay out
        m=int(round(255*ring*colour))
        line+=bytes([r,g,b,m])
    rows.append(bytes(line))
raw=b''.join(rows)
def chunk(t,c): return struct.pack('>I',len(c))+t+c+struct.pack('>I',zlib.crc32(t+c)&0xffffffff)
png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b'')
open(dst,'wb').write(png)
lum=[0.2126*px[y][x*4]+0.7152*px[y][x*4+1]+0.0722*px[y][x*4+2] for x,y in cand]
print('average iris luminance %.1f'%(sum(lum)/len(lum)))
