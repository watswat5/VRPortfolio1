"""Rebuild the texture-free, metre-scale, Y-up ladder using Python's standard library.
Pillow is optional and is used only for the preview image.
"""
import json, math, struct
from pathlib import Path

OUT = Path(__file__).resolve().parent
materials = [('Steel', (0.24, 0.29, 0.32), 0.72, 0.65),
             ('Mounts', (0.13, 0.17, 0.19), 0.65, 0.75),
             ('Fasteners', (0.48, 0.53, 0.55), 0.8, 0.45)]
parts = []

def beam(name, a, b, width, depth, material=0):
    axis = [b[i]-a[i] for i in range(3)]
    length = math.sqrt(sum(v*v for v in axis))
    y = [v/length for v in axis]
    ref = (0, 0, 1) if abs(y[2]) < .9 else (1, 0, 0)
    x = [y[1]*ref[2]-y[2]*ref[1], y[2]*ref[0]-y[0]*ref[2], y[0]*ref[1]-y[1]*ref[0]]
    n = math.sqrt(sum(v*v for v in x)); x = [v/n for v in x]
    z = [x[1]*y[2]-x[2]*y[1], x[2]*y[0]-x[0]*y[2], x[0]*y[1]-x[1]*y[0]]
    center = [(a[i]+b[i])/2 for i in range(3)]
    verts = [[center[i]+sx*width/2*x[i]+sy*length/2*y[i]+sz*depth/2*z[i] for i in range(3)]
             for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    faces = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]
    parts.append((name, verts, faces, material))

for side in (-1, 1):
    x = side*.32
    beam(f'Rail_{side}', (x,0,0), (x,4.25,0), .065,.065)
    beam(f'Handrail_flare_{side}', (x,4.23,0), (side*.40,4.52,0), .065,.065)
    beam(f'Handrail_upper_{side}', (side*.40,4.50,0), (side*.40,5.15,0), .065,.065)
    beam(f'Roof_return_{side}', (side*.40,5.12,0), (side*.40,5.12,.40), .065,.065)
    beam(f'Return_end_{side}', (side*.40,5.14,.38), (side*.40,4.91,.38), .065,.065)
    for level in (.45,1.65,2.85,3.95):
        beam(f'Wall_standoff_{side}_{level}', (x,level,0), (x,level,.27), .045,.045,1)
        beam(f'Wall_plate_{side}_{level}', (x,level-.12,.27), (x,level+.12,.27), .14,.025,1)
        for dy in (-.075,.075):
            beam('Bolt', (x,level+dy,.242), (x,level+dy,.259), .027,.027,2)
for index in range(14):
    level=.30+index*.30
    beam(f'Rung_{index+1:02}', (-.32,level,0), (.32,level,0), .04,.055)

obj=['# Roof access ladder; metres, Y up; wall is at Z=0.2825', 'mtllib roof_access_ladder.mtl', 's off']
offset=1
for name, verts, faces, mat in parts:
    obj += ['o '+name, 'usemtl '+materials[mat][0]]
    obj += ['v '+' '.join(f'{v:.6f}' for v in p) for p in verts]
    obj += ['f '+' '.join(str(offset+i) for i in face) for face in faces]
    offset += len(verts)
(OUT/'roof_access_ladder.obj').write_text('\n'.join(obj)+'\n')
(OUT/'roof_access_ladder.mtl').write_text('\n'.join(f'newmtl {name}\nKd {r} {g} {b}\nKs 0.4 0.4 0.4\nNs 40\n' for name,(r,g,b),_,_ in materials))

blob=bytearray(); views=[]; accessors=[]; primitives=[]
def accessor(values, kind, target):
    while len(blob)%4: blob.append(0)
    start=len(blob)
    flat=[v for row in values for v in row]
    blob.extend(struct.pack('<'+'f'*len(flat),*flat))
    views.append(dict(buffer=0,byteOffset=start,byteLength=len(blob)-start,target=target))
    accessors.append(dict(bufferView=len(views)-1,componentType=5126,count=len(values),type=kind,
                          min=[min(row[i] for row in values) for i in range(3)],
                          max=[max(row[i] for row in values) for i in range(3)]))
    return len(accessors)-1
for mat in range(len(materials)):
    positions=[]; normals=[]
    for _,verts,faces,m in parts:
        if m!=mat: continue
        for f in faces:
            a,b,c=[verts[i] for i in f[:3]]
            u=[b[i]-a[i] for i in range(3)]; v=[c[i]-a[i] for i in range(3)]
            normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
            length=math.sqrt(sum(t*t for t in normal)); normal=[t/length for t in normal]
            for i in (f[0],f[1],f[2],f[0],f[2],f[3]): positions.append(verts[i]); normals.append(normal)
    primitives.append(dict(attributes=dict(POSITION=accessor(positions,'VEC3',34962),NORMAL=accessor(normals,'VEC3',34962)),material=mat,mode=4))
doc=dict(asset=dict(version='2.0',generator='Roof access ladder generator'),scene=0,scenes=[dict(nodes=[0])],
         nodes=[dict(mesh=0,name='Roof Access Ladder')],meshes=[dict(primitives=primitives)],
         materials=[dict(name=n,pbrMetallicRoughness=dict(baseColorFactor=[*c,1],metallicFactor=m,roughnessFactor=r)) for n,c,m,r in materials],
         buffers=[dict(byteLength=len(blob))],bufferViews=views,accessors=accessors)
data=json.dumps(doc,separators=(',',':')).encode()
data+=b' '*((-len(data))%4)
glb=struct.pack('<III',0x46546c67,2,12+8+len(data)+8+len(blob))+struct.pack('<II',len(data),0x4e4f534a)+data+struct.pack('<II',len(blob),0x004e4942)+blob
(OUT/'roof_access_ladder.glb').write_bytes(glb)
print(f'{len(parts)} parts; {len(parts)*12} triangles; {len(glb)} GLB bytes')

try:
    from PIL import Image, ImageDraw
    image=Image.new('RGB',(1000,1200),'#e8ecee'); draw=ImageDraw.Draw(image)
    def project(p):
        x,y,z=p
        return (500+240*(.90*x+.44*z),1090-190*y+70*(-.44*x+.90*z))
    polygons=[]
    for _,verts,faces,mat in parts:
        for face in faces:
            points=[verts[i] for i in face]
            depth=sum(.44*p[0]-.90*p[2]+.2*p[1] for p in points)/4
            a,b,c=points[:3]; u=[b[i]-a[i] for i in range(3)]; v=[c[i]-a[i] for i in range(3)]
            n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
            l=math.sqrt(sum(t*t for t in n)); light=.70+.3*abs(sum(n[i]*(-.4,.8,-.45)[i] for i in range(3))/l)
            color=tuple(int(255*t*light) for t in materials[mat][1])
            polygons.append((depth,[project(p) for p in points],color))
    for _,polygon,color in sorted(polygons,key=lambda p:p[0]): draw.polygon(polygon,fill=color)
    draw.text((60,45),'ROOF ACCESS LADDER',fill='#26343b',font_size=32)
    draw.text((60,90),f'LOW POLY  /  {len(parts)*12} TRIANGLES  /  5.15 M',fill='#53636b',font_size=19)
    image.save(OUT/'preview.png')
except ImportError:
    pass
