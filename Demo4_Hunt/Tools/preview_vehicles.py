"""Render geometry read back from the exported FBX files for visual verification."""
from pathlib import Path
import math
from PIL import Image,ImageDraw
from vehicle_fbx import read

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Models/Vehicles'
im=Image.new('RGB',(1500,850),'#d6c5a9');draw=ImageDraw.Draw(im)
draw.rectangle((0,0,1500,105),fill='#243b39')
draw.text((40,23),'ROADSIDE / LOW-POLY VEHICLES',font_size=32,fill='#eee3cc')
draw.text((40,67),'FBX geometry preview  |  separate body paint  |  individual wheel pivots',font_size=19,fill='#b8c4b8')
depth=[float('-inf')]*(1500*850);pixels=im.load()
view=(.57,.4,.714)
for kind,cx in [('Pickup',380),('Sedan',1120)]:
    nodes=read(OUT/(kind+'.fbx'))
    objects=next(n[2] for n in nodes if n[0]=='Objects');byid={n[1][0]:n for n in objects}
    links=[n[1][1:] for n in next(n[2] for n in nodes if n[0]=='Connections')]
    def child(n,name):return next(c for c in n[2] if c[0]==name)
    def project(p):
        x,y,z=p
        return (cx+125*(.78*x-.625*z),620-125*(-.25*x+.916*y-.312*z))
    draw.ellipse((cx-295,557,cx+280,675),fill='#bbaa8d')
    for geo in (n for n in objects if n[0]=='Geometry'):
        model_id=next(b for a,b in links if a==geo[1][0]);model=byid[model_id]
        pos=next(c[1][4:] for c in child(model,'Properties70')[2] if c[1][0]=='Lcl Translation')
        mats=[byid[a] for a,b in links if b==model_id and byid[a][0]=='Material']
        colors=[next(c[1][4:] for c in child(mat,'Properties70')[2] if c[1][0]=='DiffuseColor') for mat in mats]
        flat=child(geo,'Vertices')[1][0][1]
        vertices=[tuple(flat[i+j]+pos[j] for j in range(3)) for i in range(0,len(flat),3)]
        indices=child(geo,'PolygonVertexIndex')[1][0][1]
        mi=child(child(geo,'LayerElementMaterial'),'Materials')[1][0][1]
        for f in range(len(mi)):
            ids=indices[f*3:f*3+3];ids=[i if i>=0 else -i-1 for i in ids]
            points=[vertices[i] for i in ids];a,b,c=points
            u=[b[i]-a[i] for i in range(3)];v=[c[i]-a[i] for i in range(3)]
            n=(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
            length=math.sqrt(sum(t*t for t in n));n=tuple(t/length for t in n)
            if sum(n[i]*view[i] for i in range(3))<=0:continue
            light=.69+.31*max(0,sum(n[i]*(.25,.85,.45)[i] for i in range(3)))
            color=tuple(int(t*255*light) for t in colors[mi[f]])
            a,b,c=[(*project(p),sum(p[i]*view[i] for i in range(3))) for p in points]
            denom=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(denom)<1e-8:continue
            for y in range(max(106,int(min(a[1],b[1],c[1]))),min(740,int(max(a[1],b[1],c[1]))+1)):
                for x in range(max(0,int(min(a[0],b[0],c[0]))),min(1500,int(max(a[0],b[0],c[0]))+1)):
                    u=((b[1]-c[1])*(x+.5-c[0])+(c[0]-b[0])*(y+.5-c[1]))/denom
                    v=((c[1]-a[1])*(x+.5-c[0])+(a[0]-c[0])*(y+.5-c[1]))/denom;w=1-u-v
                    if min(u,v,w)<0:continue
                    z=u*a[2]+v*b[2]+w*c[2];offset=y*1500+x
                    if z>depth[offset]:depth[offset]=z;pixels[x,y]=color
    draw.text((cx-270,178),kind.upper(),font_size=32,fill='#293c36')
    draw.text((cx-270,224),'986 triangles / open bed' if kind=='Pickup' else '906 triangles / four doors',font_size=20,fill='#72624e')
draw.rectangle((0,770,1500,850),fill='#243b39')
draw.text((40,790),'Change Pickup_BodyPaint or Sedan_BodyPaint to recolor the body.',font_size=22,fill='#eee3cc')
draw.text((40,822),'Geometry read from exported FBX files. Preview lighting is illustrative.',font_size=15,fill='#b8c4b8')
im.save(OUT/'VehiclesPreview.png')
print('Rendered the exported FBX geometry to VehiclesPreview.png')
