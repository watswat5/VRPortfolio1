"""Dependency-free CRT geometry generator. Unity coordinates: front -Z, up +Y, metres."""
import json, math
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Models/CartoonCRT'
OUT.mkdir(parents=True, exist_ok=True)
parts = []
colors = {'Burgundy': [.36,.075,.105], 'Pink': [.68,.36,.43], 'Dark': [.045,.055,.065], 'Steel': [.30,.34,.37], 'Screen': [.065,.12,.13]}
def mesh(name, mat, verts, faces, uv=None):
    parts.append(dict(name=name, material=mat, vertices=[dict(x=x,y=y,z=z) for x,y,z in verts], triangles=[i for f in faces for i in f], uv=[dict(x=u,y=v) for u,v in (uv or [(0,0)]*len(verts))]))
def outline(w,h,r,cx,cy,z):
    v=[]
    for x,y,a in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
        for j in range(9):
            t=math.radians(a+j*90/8)
            v.append((cx+x+r*math.cos(t),cy+y+r*math.sin(t),z))
    return v
def rings(name,mat,spec,cx,cy,cap=False):
    v=[]; f=[]; n=36
    for s in spec:
        w,h,r,z=s[:4]
        v+=outline(w,h,r,s[4] if len(s)>4 else cx,cy,z)
    for k in range(len(spec)-1):
        for j in range(n):
            a=k*n+j;b=k*n+(j+1)%n;c=b+n;d=a+n
            f.extend([(a,b,c),(a,c,d)])
    if cap:
        for j in range(1,n-1): f.append((0,j+1,j))
        o=(len(spec)-1)*n
        for j in range(1,n-1): f.append((o,o+j,o+j+1))
    if not cap: f=[(a,c,b) for a,b,c in f]
    mesh(name,mat,v,f)
def block(name,mat,x,y,z,w,h,d,r):
    b=min(.008,d/3,r/3)
    rings(name,mat,[(w-2*b,h-2*b,max(.001,r-b),z-d/2),(w,h,r,z-d/2+b),(w,h,r,z+d/2-b),(w-2*b,h-2*b,max(.001,r-b),z+d/2)],x,y,True)
def ball(name,mat,c,s):
    v=[];f=[]
    for j in range(13):
        a=math.pi*j/12
        for i in range(24):
            t=2*math.pi*i/24
            v.append((c[0]+s[0]*math.sin(a)*math.cos(t),c[1]+s[1]*math.cos(a),c[2]+s[2]*math.sin(a)*math.sin(t)))
    for j in range(12):
        for i in range(24):
            a=j*24+i;b=j*24+(i+1)%24
            if j>0:f.append((a,b,a+24))
            if j<11:f.append((b,b+24,a+24))
    mesh(name,mat,v,f)
def rod(name,a,b,r):
    # Antenna is in the XY plane; perpendicular bases are Z and cross(direction,Z).
    dx,dy=b[0]-a[0],b[1]-a[1]; l=math.hypot(dx,dy);v=[];f=[]
    for p in (a,b):
        for i in range(16):
            t=2*math.pi*i/16;v.append((p[0]+dy/l*r*math.sin(t),p[1]-dx/l*r*math.sin(t),p[2]+r*math.cos(t)))
    for i in range(16):
        j=(i+1)%16;f.extend([(i,j,j+16),(i,j+16,i+16)])
    mesh(name,'Steel',v,f)
# Deep cabinet stops behind the screen; front surround has a real opening.
block('Cabinet','Burgundy',0,.285,.055,.60,.45,.31,.038)
# Screen center is offset left to make room for radio-like controls.
cx=-.061; cy=.292
# Outer ring shares screen center; 0.60m overall width.
rings('FrontSurround','Burgundy',[(.60,.45,.038,-.106),(.60,.45,.038,-.158),(.424,.326,.048,-.158,cx),(.416,.318,.046,-.138,cx)],0,.285)
# Asymmetric control panel overlays right part of opening; screen rim is offset left.
block('ControlPanel','Burgundy',.226,.285,-.133,.14,.416,.061,.016)
rings('PinkBezel','Pink',[(.448,.348,.057,-.161),(.436,.336,.052,-.172),(.392,.294,.041,-.172),(.384,.288,.038,-.142)],cx,cy)
rings('InnerGasket','Dark',[(.384,.288,.038,-.142),(.376,.282,.034,-.136)],cx,cy)
v=outline(.378,.2835,.035,cx,cy,-.135)
v.append((cx,cy,-.135));f=[(36,(i+1)%36,i) for i in range(36)]
mesh('Screen_RenderTexture','Screen',v,f,[((x-cx)/.378+.5,(y-cy)/.2835+.5) for x,y,z in v])
for i,y in enumerate([.40,.303]):
    block('DialSocket'+str(i),'Dark',.221,y,-.17,.078,.078,.022,.039)
    block('PinkDial'+str(i),'Pink',.221,y,-.191,.065,.065,.034,.0325)
    block('DialMarker'+str(i),'Burgundy',.221,y+.016,-.211,.007,.020,.003,.003)
for i in range(3):
    ball('SpeakerPill'+str(i),'Pink',(.221,.208-i*.033,-.178),(.048,.009,.010))
for x in [-.21,.21]:
    for z in [-.085,.14]:block('Foot'+str(x)+str(z),'Dark',x,.033,z,.07,.066,.067,.015)
ball('AntennaMount','Dark',(.11,.508,.075),(.047,.018,.037))
rod('Antenna',(.11,.513,.075),(.20,.76,.075),.008)
ball('AntennaTip','Steel',(.20,.76,.075),(.025,.025,.025))
for i in range(5):block('RearVent'+str(i),'Dark',-.10+i*.05,.30,.212,.012,.15,.006,.006)
data={'parts':parts,'colors':colors}
(OUT/'CartoonCRT.geometry.json').write_text(json.dumps(data),encoding='utf8')
lines=['# Cartoon CRT; metres; Y up; front -Z','mtllib CartoonCRT.mtl'];off=1
for p in parts:
    lines += ['o '+p['name'],'usemtl '+p['material']]
    lines += ['v {x:.7f} {y:.7f} {z:.7f}'.format(**v) for v in p['vertices']]
    lines += ['vt {x:.7f} {y:.7f}'.format(**v) for v in p['uv']]
    t=p['triangles']
    lines += ['f '+' '.join(f'{off+j}/{off+j}' for j in t[i:i+3]) for i in range(0,len(t),3)]
    off+=len(p['vertices'])
(OUT/'CartoonCRT.obj').write_text('\n'.join(lines),encoding='utf8')
(OUT/'CartoonCRT.mtl').write_text('\n'.join(f'newmtl {n}\nKd {c[0]} {c[1]} {c[2]}\nKs 0.05 0.05 0.05\nNs 20\n' for n,c in colors.items()),encoding='utf8')
print(f'{len(parts)} parts, {sum(len(p["triangles"])//3 for p in parts)} triangles')
