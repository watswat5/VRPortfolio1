"""Rebuild the existing cave OBJ in place; preserve its GUID and scene placement."""
from pathlib import Path
from collections import Counter
import math
import re
import uuid

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Environment/Highway/CavePortal'
def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL,'scavenger/cave-ridge/'+name).hex
palette={'Sandstone':(.73,.425,.245),'Sandstone Sun':(.81,.512325,.2997),
         'Sandstone Shade':(.64,.345,.195),'Cave Black':(0,0,0)}
vertices=[]
triangles=[]
def vertex(p):
    vertices.append(tuple(p))
    return len(vertices)-1
def normal(a,b,c):
    p,q,r=(vertices[i] for i in (a,b,c))
    u=[q[i]-p[i] for i in range(3)];v=[r[i]-p[i] for i in range(3)]
    return (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
def triangle(a,b,c,material,direction):
    n=normal(a,b,c)
    assert sum(t*t for t in n)>1e-12
    if sum(n[i]*direction[i] for i in range(3))<0:b,c=c,b
    triangles.append((a,b,c,material))

# Broad asymmetric mesa silhouette: a continuous closed rock mass, not separate rocks.
xs=[-28,-23,-17,-12,-6,0,6,12,18,24,28]
heights=[.18,4.5,9.2,12.6,13,14.8,13.5,13.9,9.3,4.1,.18]
fronts=[-4,-2.6,-1.2,-.6,0,0,0,-.8,-1.3,-2.9,-4.2]
columns=[]
for x,h,z in zip(xs,heights,fronts):
    columns.append([vertex(p) for p in ((x,-.1,z),(x,h*.43,z),(x,h,z-1.6),
                                       (x,h*.82,z-7),(x,-.1,z-13))])
directions=[(0,0,1),(0,0,1),(0,1,0),(0,0,-1),(0,-1,0)]
for i in range(len(columns)-1):
    for j in range(5):
        a,b=columns[i][j],columns[i+1][j]
        c,d=columns[i+1][(j+1)%5],columns[i][(j+1)%5]
        mat='Sandstone Sun' if j==2 else 'Sandstone'
        if j==3 or (j==1 and i in (1,6,8)):mat='Sandstone Shade'
        triangle(a,b,c,mat,directions[j]);triangle(a,c,d,mat,directions[j])
for col,direction in ((columns[0],(-1,0,0)),(columns[-1],(1,0,0))):
    for j in range(1,4):triangle(col[0],col[j],col[j+1],'Sandstone Shade',direction)
ridge_count=len(triangles)

# Painted cartoon tunnel: 11.4m wide, 7.7m tall. No collider blocks the roadway.
# Vehicles can pass behind this visual occluder to the existing hidden triggers.
outline=[(-5.7,-.06,.025),(5.7,-.06,.025),(5.7,3.7,.025)]
for i in range(1,13):
    angle=math.pi*i/12
    outline.append((5.7*math.cos(angle),3.7+4*math.sin(angle),.025))
indices=[vertex(p) for p in outline]
center=vertex((0,3,.025))
for i in range(len(indices)):triangle(center,indices[i],indices[(i+1)%len(indices)],'Cave Black',(0,0,1))

# Check manifold edges and outward winding before export.
edges=Counter();volume=0
for a,b,c,_ in triangles[:ridge_count]:
    for u,v in ((a,b),(b,c),(c,a)):edges[(u,v)]+=1
    p,q,r=(vertices[i] for i in (a,b,c))
    volume+=sum(p[i]*(q[(i+1)%3]*r[(i+2)%3]-q[(i+2)%3]*r[(i+1)%3]) for i in range(3))/6
assert all(n==1 and edges[(v,u)]==1 for (u,v),n in edges.items()),'Open or reversed face'
assert volume>0,'Inside-out ridge'
normals=[]
for a,b,c,_ in triangles:
    n=normal(a,b,c);length=math.sqrt(sum(t*t for t in n))
    normals.append(tuple(t/length for t in n))
obj=['# Continuous desert ridge. Y up; front +Z.','mtllib cave_portal.mtl','o Desert_Ridge','s off']
obj+=['v %.6f %.6f %.6f'%p for p in vertices]
obj+=['vn %.7f %.7f %.7f'%n for n in normals]
last=None
for i,(a,b,c,mat) in enumerate(triangles):
    if mat!=last:obj.append('usemtl '+mat);last=mat
    obj.append('f '+' '.join(f'{v+1}//{i+1}' for v in (a,b,c)))
(OUT/'cave_portal.obj').write_text('\n'.join(obj)+'\n')
(OUT/'cave_portal.mtl').write_text('\n'.join(f'newmtl {name}\nKd {r} {g} {b}\nKs 0 0 0\nNs 1\nillum 1\n' for name,(r,g,b) in palette.items()))

# Remap imported materials explicitly to URP; the hole uses a solid black unlit shader.
template=(ROOT/'Assets/Materials/desert.mat').read_text().split('--- !u!114')[0]
for name,color in palette.items():
    content=template.replace('m_Name: desert','m_Name: '+name)
    for prop in ('_BaseColor','_Color'):
        content=re.sub(r'('+prop+r': )\{[^}]+\}',lambda m:m[1]+'{r: %s, g: %s, b: %s, a: 1}'%color,content)
    content=re.sub(r'(_Smoothness: )[^\n]+',r'\g<1>0',content)
    content=re.sub(r'(_Metallic: )[^\n]+',r'\g<1>0',content)
    if name=='Cave Black':
        content=re.sub(r'm_Shader: \{[^}]+\}','m_Shader: {fileID: 4800000, guid: '+guid('CaveBlack.shader')+', type: 3}',content)
    path=OUT/(name+'.mat');path.write_text(content)
    path.with_suffix('.mat.meta').write_text('fileFormatVersion: 2\nguid: '+guid(name)+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n')
meta_path=OUT/'cave_portal.obj.meta';meta=meta_path.read_text()
mapping='  externalObjects:\n'
for name in palette:
    mapping+=('  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n'+
              f'      name: {name}\n    second: {{fileID: 2100000, guid: {guid(name)}, type: 2}}\n')
meta=re.sub(r'  externalObjects:.*?(?=  materials:)',lambda m:mapping,meta,flags=re.S)
meta_path.write_text(meta)
(OUT/'CaveBlack.shader.meta').write_text('fileFormatVersion: 2\nguid: '+guid('CaveBlack.shader')+'\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n')
print(f'Validated closed outward-facing ridge: {ridge_count} triangles. Total: {len(triangles)} triangles.')

# A geometry preview, not a screenshot of Unity lighting.
from PIL import Image,ImageDraw
im=Image.new('RGB',(1200,650),'#d4b389');draw=ImageDraw.Draw(im)
def project(p):
    x,y,z=p
    return (620+18*(.98*x+.20*z),535-18*(.94*y-.075*x-.33*z))
view=(-.188,.3084,.9212)
pixels=im.load()
zbuffer=[float('-inf')]*(1200*650)
for index,(a,b,c,mat) in enumerate(triangles):
    n=normals[index]
    if sum(n[i]*view[i] for i in range(3))<=0:continue
    light=.78+.22*max(0,sum(n[i]*(-.3,.8,.5)[i] for i in range(3)))
    color=tuple(int(t*255*light) for t in palette[mat])
    p,q,r=[(*project(vertices[i]),sum(vertices[i][j]*view[j] for j in range(3))) for i in (a,b,c)]
    denominator=(q[1]-r[1])*(p[0]-r[0])+(r[0]-q[0])*(p[1]-r[1])
    if abs(denominator)<1e-8:continue
    for y in range(max(0,int(min(p[1],q[1],r[1]))),min(650,int(max(p[1],q[1],r[1]))+1)):
        for x in range(max(0,int(min(p[0],q[0],r[0]))),min(1200,int(max(p[0],q[0],r[0]))+1)):
            u=((q[1]-r[1])*(x+.5-r[0])+(r[0]-q[0])*(y+.5-r[1]))/denominator
            v=((r[1]-p[1])*(x+.5-r[0])+(p[0]-r[0])*(y+.5-r[1]))/denominator
            w=1-u-v
            if min(u,v,w)<0:continue
            z=u*p[2]+v*q[2]+w*r[2];offset=y*1200+x
            if z>zbuffer[offset]:zbuffer[offset]=z;pixels[x,y]=color
draw.text((35,25),'DESERT RIDGE / CARTOON TUNNEL',font_size=28,fill='#493125')
draw.text((35,66),'Single continuous ridge. Flat facets. Unlit black opening.',font_size=19,fill='#66462f')
draw.text((35,610),'Geometry preview - desert palette matched to the project ground material',font_size=16,fill='#66462f')
im.save(OUT/'RidgePreview.png')
