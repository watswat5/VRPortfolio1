"""Render a daylight geometry preview of the proposed scene layout (not a Unity screenshot)."""
from pathlib import Path
import json, math
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
nodes=json.loads((ROOT/'Temp/highway-layout.json').read_text())
colors={'asphalt':(.12,.13,.13),'steel':(.604,.71,.71),'white':(.78,.75,.64),
        'yellow':(.84,.57,.12),'shoulder':(.38,.30,.22),'concrete':(.46,.44,.37)}
faces=[]
def poly(points,color):
    faces.append((points,color))
def box(center,scale,color):
    verts=[tuple(center[i]+sign[i]*scale[i]/2 for i in range(3)) for sign in
           [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]:poly([verts[i] for i in f],color)
box((4.55,.004,-4.113),(24.52,.012,12.702),colors['asphalt'])
for n in nodes:
    if n['mat']:box(n['position'],n['scale'],colors[n['mat']])
def obj(path,position):
    palette={};current='';verts=[]
    for line in path.with_suffix('.mtl').read_text().splitlines():
        t=line.split()
        if t and t[0]=='newmtl':current=t[1]
        if t and t[0]=='Kd':palette[current]=tuple(map(float,t[1:4]))
    for line in path.read_text().splitlines():
        t=line.split()
        if not t:continue
        if t[0]=='v':
            x,z,y=map(float,t[1:4]);verts.append((x+position[0],y+position[1],z+position[2]))
        if t[0]=='usemtl':current=t[1]
        if t[0]=='f':poly([verts[int(i.split('/')[0])-1] for i in t[1:]],palette.get(current,(.4,.4,.4)))
obj(ROOT/'Assets/Models/motel/motel.obj',(4.513,0,6.374))
obj(ROOT/'Assets/Models/motel_sign/motel_sign.obj',(-10.59,0,-7.4))

im=Image.new('RGB',(1500,1080),(167,126,91));d=ImageDraw.Draw(im)
def project(p):
    x,y,z=p;x-=4;z+=8
    return (750+25*(.866*x+.5*z),590-25*(-.25*x+.866*y+.433*z))
def depth(points):return sum(.433*p[0]+.5*p[1]-.75*p[2] for p in points)/len(points)
# Rasterize depth so the large asphalt faces cannot occlude the paint by sort order.
pixels=im.load()
zbuffer=[float('-inf')]*(1500*1080)
for pts,color in faces:
    a,b,c=pts[:3];u=[b[i]-a[i] for i in range(3)];v=[c[i]-a[i] for i in range(3)]
    n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
    length=math.sqrt(sum(t*t for t in n)) or 1
    shade=.65+.35*abs(sum(n[i]*(-.4,.85,-.3)[i] for i in range(3))/length)
    fill=tuple(int(255*t*shade) for t in color)
    projected=[(*project(p),.433*p[0]+.5*p[1]-.75*p[2]) for p in pts]
    for i in range(1,len(pts)-1):
        a,b,c=projected[0],projected[i],projected[i+1]
        denominator=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(denominator)<1e-8:continue
        for y in range(max(104,int(min(a[1],b[1],c[1]))),min(1026,int(max(a[1],b[1],c[1]))+1)):
            for x in range(max(0,int(min(a[0],b[0],c[0]))),min(1500,int(max(a[0],b[0],c[0]))+1)):
                u=((b[1]-c[1])*(x+.5-c[0])+(c[0]-b[0])*(y+.5-c[1]))/denominator
                v=((c[1]-a[1])*(x+.5-c[0])+(a[0]-c[0])*(y+.5-c[1]))/denominator
                w=1-u-v
                if min(u,v,w)<0:continue
                z=u*a[2]+v*b[2]+w*c[2];index=y*1500+x
                if z>zbuffer[index]:zbuffer[index]=z;pixels[x,y]=fill
d.rectangle((0,0,1500,104),fill='#202c2c')
d.text((45,26),'MOTEL / HIGHWAY CONNECTION',font_size=31,fill='#ede1c6')
d.text((45,68),'Geometry preview  |  8 parking bays  |  8 m driveway  |  steel shoulder barriers',font_size=18,fill='#c4c7bb')
d.rectangle((0,1026,1500,1080),fill='#202c2c')
d.text((45,1042),'Daylight layout study — existing night lighting and sky remain in the Unity scene.',font_size=18,fill='#c4c7bb')
im.save(ROOT/'Temp/highway-preview.png')

# Top-down view for checking the pedestrian frontage and unobstructed vehicle aisle.
im=Image.new('RGB',(1250,1200),'#aa805b');d=ImageDraw.Draw(im)
def plan(x,z):return (625+(x-4.5)*24,170+(14-z)*24)
def rect(x0,z0,x1,z1,col):
    a=plan(x0,z1);b=plan(x1,z0);d.rectangle((*a,*b),fill=col)
rect(-7.71,-10.46,16.81,2.24,'#303332')
rect(-7.9,2,16.9,10.8,'#777c70')
rect(-5.74,.324,16.76,2.324,'#b8aa8d')
for n in sorted((n for n in nodes if n['mat']),key=lambda n:n['position'][1]):
    x,y,z=n['position'];w,h,l=n['scale'];col=tuple(int(t*255) for t in colors[n['mat']])
    rect(x-w/2,z-l/2,x+w/2,z+l/2,col)
d.rectangle((0,0,1250,115),fill='#202c2c')
d.text((40,24),'SITE PLAN',font_size=31,fill='#ede1c6')
d.text((40,72),'Existing motel and parking surface retained',font_size=20,fill='#c4c7bb')
for text,x,z in [('MOTEL',1,6),('5.5 m maneuvering aisle',-2,-7),('ENTRY',9.5,-11.5)]:
    d.text(plan(x,z),text,font_size=18,fill='#e9ddc3')
im.save(ROOT/'Temp/highway-plan.png')
print('Wrote geometry preview and site plan to Temp.')
