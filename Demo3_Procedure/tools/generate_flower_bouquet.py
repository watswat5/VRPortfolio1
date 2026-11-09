"""Generate a portable flat-shaded bouquet and its Unity mesh data. No dependencies."""
import json
import math
import pathlib
import struct
import zlib

OUT = pathlib.Path(__file__).resolve().parents[1] / 'Assets/Models/FlowerBouquet'
OUT.mkdir(parents=True, exist_ok=True)
COLORS = {
    'Ceramic': (0.19, 0.51, 0.52), 'Rim': (0.38, 0.69, 0.66),
    'Inside': (0.12, 0.32, 0.33), 'Stem': (0.23, 0.40, 0.19),
    'Leaf': (0.32, 0.53, 0.25), 'Coral': (0.95, 0.36, 0.32),
    'Peach': (1.0, 0.65, 0.43), 'Cream': (1.0, 0.91, 0.70),
    'Pink': (0.87, 0.42, 0.60), 'Gold': (0.96, 0.68, 0.18),
}
parts = []
def add(a, b): return tuple(x+y for x, y in zip(a, b))
def sub(a, b): return tuple(x-y for x, y in zip(a, b))
def mul(a, s): return tuple(x*s for x in a)
def dot(a, b): return sum(x*y for x, y in zip(a, b))
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def unit(a): return mul(a, 1/math.sqrt(dot(a, a)))
def basis(n):
    u = unit(cross((0, 0, 1) if abs(n[2]) < .9 else (1, 0, 0), n))
    return u, cross(n, u), n
def part(name, material):
    p = dict(name=name, material=material, vertices=[], triangles=[], uv=[])
    parts.append(p)
    return p
def tri(p, a, b, c, outward=None):
    if outward is not None and dot(cross(sub(b,a), sub(c,a)), outward) < 0: b,c=c,b
    start = len(p['vertices'])
    p['vertices'].extend(dict(zip(('x','y','z'),v)) for v in (a,b,c))
    p['triangles'].extend((start,start+1,start+2))
    p['uv'].extend((dict(x=0,y=0),dict(x=1,y=0),dict(x=.5,y=1)))
def ellipsoid(p, center, axes, radius, sides=7):
    def point(phi, theta):
        local = (math.sin(phi)*math.cos(theta)*radius[0], math.sin(phi)*math.sin(theta)*radius[1], math.cos(phi)*radius[2])
        return add(center, add(mul(axes[0],local[0]),add(mul(axes[1],local[1]),mul(axes[2],local[2]))))
    rings = [[point(phi, 2*math.pi*i/sides) for i in range(sides)] for phi in (math.pi/3,2*math.pi/3)]
    top, bottom = point(0,0), point(math.pi,0)
    def face(a,b,c): tri(p,a,b,c,sub(mul(add(add(a,b),c),1/3),center))
    for i in range(sides):
        j=(i+1)%sides
        face(top,rings[0][i],rings[0][j])
        face(rings[0][i],rings[1][i],rings[1][j])
        face(rings[0][i],rings[1][j],rings[0][j])
        face(bottom,rings[1][j],rings[1][i])
def tube(p,a,b,r):
    u,v,n=basis(unit(sub(b,a)))
    rings=[[add(c,add(mul(u,r*math.cos(i*math.tau/5)),mul(v,r*math.sin(i*math.tau/5)))) for i in range(5)] for c in (a,b)]
    for i in range(5):
        j=(i+1)%5
        outward=sub(rings[0][i],a)
        tri(p,rings[0][i],rings[1][i],rings[1][j],outward)
        tri(p,rings[0][i],rings[1][j],rings[0][j],outward)
        tri(p,a,rings[0][j],rings[0][i],mul(n,-1))
        tri(p,b,rings[1][i],rings[1][j],n)

# A closed ceramic cross section with an open mouth and an inner wall.
profile=[(.055,0),(.076,.018),(.087,.09),(.077,.165),(.050,.218),(.052,.238),(.041,.238),(.039,.218),(.065,.16),(.073,.09),(.061,.024),(0,.024),(0,0),(.055,0)]
vase=part('Vase','Ceramic'); rim=part('Vase_Rim','Rim'); inside=part('Vase_Interior','Inside')
for k,((r0,y0),(r1,y1)) in enumerate(zip(profile,profile[1:])):
    p=rim if k in (4,5) else inside if 6<=k<=10 else vase
    for i in range(10):
        t0,t1=i*math.tau/10,(i+1)*math.tau/10
        a=(r0*math.cos(t0),y0,r0*math.sin(t0)); b=(r1*math.cos(t0),y1,r1*math.sin(t0))
        c=(r1*math.cos(t1),y1,r1*math.sin(t1)); d=(r0*math.cos(t1),y0,r0*math.sin(t1))
        if r1>0: tri(p,a,b,c)
        if r0>0: tri(p,a,c,d)

flowers=[(0,.545,0,'Cream',1.05)]
for i in range(5):
    t=i*math.tau/5+.3
    flowers.append((.088*math.cos(t),.475+.012*math.sin(t*2),.088*math.sin(t),['Coral','Peach','Pink','Cream','Coral'][i],1.0))
for i in range(4):
    t=i*math.tau/4+.7
    flowers.append((.133*math.cos(t),.404+.015*math.cos(t),.133*math.sin(t),['Peach','Cream','Pink','Coral'][i],.87))
stems=part('Stems','Stem'); leaves=part('Leaves','Leaf'); centers=part('Golden_Centers','Gold')
for f,(x,y,z,color,size) in enumerate(flowers):
    head=(x,y,z); n=unit((x*3.7,.85,z*3.7)); u,v,n=basis(n)
    base=(x*.13,.07,z*.13); neck=(x*.29,.235,z*.29)
    tube(stems,base,neck,.0035); tube(stems,neck,head,.0035)
    petals=part('Flower_%02d_%s'%(f+1,color),color)
    for i in range(7):
        t=i*math.tau/7+f*.39
        radial=add(mul(u,math.cos(t)),mul(v,math.sin(t)))
        tangent=cross(n,radial)
        c=add(head,add(mul(radial,.031*size),mul(n,.001)))
        ellipsoid(petals,c,(radial,tangent,n),(.027*size,.015*size,.009*size),6)
    ellipsoid(centers,add(head,mul(n,.009)),(u,v,n),(.019*size,.019*size,.011*size),8)
    for j in range(2):
        t=f*2.4+j*2.7
        start=add(neck,mul(sub(head,neck),.3+j*.24))
        direction=unit((math.cos(t),.48,math.sin(t)))
        side=mul(unit(cross(direction,(0,1,0))),.018)
        end=add(start,mul(direction,.077)); mid=add(start,mul(direction,.041))
        ridge=add(mid,(0,.009,0)); under=add(mid,(0,-.004,0))
        edge=[start,add(mid,side),end,sub(mid,side)]
        for q in range(4):
            tri(leaves,ridge,edge[q],edge[(q+1)%4],(0,1,0))
            tri(leaves,under,edge[(q+1)%4],edge[q],(0,-1,0))

(OUT/'FlowerBouquet.geometry.json').write_text(json.dumps(dict(parts=parts),separators=(',',':')))
lines=['# Low-poly bouquet; metres; Y up','mtllib FlowerBouquet.mtl','s off']; offset=1
for p in parts:
    lines.extend(('o '+p['name'],'usemtl '+p['material']))
    for v in p['vertices']: lines.append('v {x:.6f} {y:.6f} {z:.6f}'.format(**v))
    for i in range(0,len(p['vertices']),3):
        a,b,c=[tuple(v[k] for k in ('x','y','z')) for v in p['vertices'][i:i+3]]
        normal=unit(cross(sub(b,a),sub(c,a)))
        lines.extend(['vn %.6f %.6f %.6f'%normal]*3)
    for i in range(0,len(p['triangles']),3): lines.append('f '+' '.join('%d//%d'%(offset+n,offset+n) for n in p['triangles'][i:i+3]))
    offset+=len(p['vertices'])
(OUT/'FlowerBouquet.obj').write_text('\n'.join(lines)+'\n')
(OUT/'FlowerBouquet.mtl').write_text('\n'.join('newmtl %s\nKd %s\nKa 0.1 0.1 0.1\nKs 0 0 0\nNs 8\n'%(name,' '.join(map(str,c))) for name,c in COLORS.items()))

# Orthographic triangle rasterizer for a preview of the actual exported geometry.
W=H=800
pixels=bytearray([239,234,222])*(W*H); depth=[-1e9]*(W*H)
eye=unit((.85,.6,-1.4)); right=unit(cross((0,1,0),eye)); up=cross(eye,right)
light=unit((-.5,1,-.6)); focus=(0,.285,0); scale=1140
def project(v):
    a=sub(v,focus)
    return (W/2+dot(a,right)*scale,H/2-dot(a,up)*scale,dot(a,eye))
for p in parts:
    color=COLORS[p['material']]
    vs=[tuple(v[k] for k in ('x','y','z')) for v in p['vertices']]
    for i in range(0,len(vs),3):
        a,b,c=vs[i:i+3]; normal=cross(sub(b,a),sub(c,a))
        if dot(normal,normal)<1e-16: continue
        normal=unit(normal)
        if dot(normal,eye)<=0: continue
        intensity=.66+.34*max(0,dot(normal,light))
        rgb=bytes(round(255*min(1,x*intensity)) for x in color)
        a,b,c=map(project,(a,b,c))
        den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-9: continue
        for py in range(max(0,int(min(a[1],b[1],c[1]))),min(H,int(max(a[1],b[1],c[1]))+1)):
            for px in range(max(0,int(min(a[0],b[0],c[0]))),min(W,int(max(a[0],b[0],c[0]))+1)):
                u=((b[1]-c[1])*(px-c[0])+(c[0]-b[0])*(py-c[1]))/den
                v=((c[1]-a[1])*(px-c[0])+(a[0]-c[0])*(py-c[1]))/den
                w=1-u-v
                if min(u,v,w)<0: continue
                z=u*a[2]+v*b[2]+w*c[2]; idx=py*W+px
                if z>depth[idx]: depth[idx]=z; pixels[idx*3:idx*3+3]=rgb
def chunk(kind,data): return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
raw=b''.join(b'\x00'+pixels[y*W*3:(y+1)*W*3] for y in range(H))
(OUT/'FlowerBouquet_Preview.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',W,H,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
print('Generated',len(parts),'parts;',sum(len(p['triangles'])//3 for p in parts),'triangles in',OUT)
