"""Build two metre-scale, flat-shaded vehicles and export binary FBX 7.4."""
from pathlib import Path
import math
import re
import uuid
from vehicle_fbx import node as N, read, write

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Models/Vehicles'
OUT.mkdir(parents=True,exist_ok=True)
COLORS={'Tires':(.025,.030,.031),'Glass':(.055,.13,.15),'Trim':(.43,.48,.46),
        'DarkTrim':(.065,.078,.078),'Headlights':(.95,.85,.62),'Taillights':(.53,.055,.025),
        'Pickup_BodyPaint':(.63,.27,.16),'Sedan_BodyPaint':(.12,.35,.32)}
def guid(name):return uuid.uuid5(uuid.NAMESPACE_URL,'scavenger/vehicles/'+name).hex
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def normal(points):
    a,b,c=points[:3];n=cross([b[i]-a[i] for i in range(3)],[c[i]-a[i] for i in range(3)])
    l=math.sqrt(sum(v*v for v in n));assert l>1e-9
    return tuple(v/l for v in n)

class Mesh:
    def __init__(self,name,position=(0,0,0)):
        self.name=name;self.position=position;self.faces=[]
    def face(self,points,mat,direction):
        points=list(points)
        n=normal(points)
        if sum(n[i]*direction[i] for i in range(3))<0:points.reverse()
        for i in range(1,len(points)-1):self.faces.append(([points[0],points[i],points[i+1]],mat))
    def box(self,c,s,mat):
        v=[tuple(c[i]+p[i]*s[i]/2 for i in range(3)) for p in
           [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        for ids,d in [((0,3,2,1),(0,0,-1)),((4,5,6,7),(0,0,1)),((0,1,5,4),(0,-1,0)),
                      ((3,7,6,2),(0,1,0)),((0,4,7,3),(-1,0,0)),((1,2,6,5),(1,0,0))]:
            self.face([v[i] for i in ids],mat,d)

def ears(poly):
    # Ear clipping for the concave wheel-arch side silhouettes in (z,y).
    ids=list(range(len(poly)));result=[]
    def area(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    if sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in ids)<0:ids.reverse()
    while len(ids)>3:
        found=False
        for k,b in enumerate(ids):
            a,c=ids[k-1],ids[(k+1)%len(ids)]
            if area(poly[a],poly[b],poly[c])<=1e-10:continue
            if any(min(area(poly[a],poly[b],poly[j]),area(poly[b],poly[c],poly[j]),area(poly[c],poly[a],poly[j]))>=-1e-10
                   for j in ids if j not in (a,b,c)):continue
            result.append((a,b,c));ids.pop(k);found=True;break
        assert found,'Failed to triangulate wheel arch'
    result.append(tuple(ids));return result

def body(mesh,half_length,half_width,wheels,paint,top):
    profile=[(-half_length,.44)]
    for z in wheels:
        # Rounded wheel cutouts rise into the side of the body.
        for i in range(9):
            a=math.pi-i*math.pi/8
            profile.append((z+math.cos(a)*.46,.44+math.sin(a)*.46))
    profile += [(half_length,.44),(half_length,top-.09),(half_length-.16,top),(-half_length+.14,top),(-half_length,top-.09)]
    for side in (-1,1):
        for tri in ears(profile):
            mesh.face([(side*half_width,profile[i][1],profile[i][0]) for i in tri],paint,(side,0,0))
    for i,(z,y) in enumerate(profile):
        nz,ny=profile[(i+1)%len(profile)]
        mesh.face([(-half_width,y,z),(half_width,y,z),(half_width,ny,nz),(-half_width,ny,nz)],paint,(0,-(nz-z),ny-y))

def cabin(mesh,back,roofback,rooffront,front,base,height,bottom_width,roof_width,paint,sedan):
    # Four trapezoidal faces and a roof; glass is inset from every pillar.
    b=[(-bottom_width,base,back),(bottom_width,base,back),(bottom_width,base,front),(-bottom_width,base,front)]
    t=[(-roof_width,height,roofback),(roof_width,height,roofback),(roof_width,height,rooffront),(-roof_width,height,rooffront)]
    mesh.face(t,paint,(0,1,0))
    for i,d in [(0,(0,0,-1)),(1,(1,0,0)),(2,(0,0,1)),(3,(-1,0,0))]:
        mesh.face([b[i],b[(i+1)%4],t[(i+1)%4],t[i]],paint,d)
    def lerp(a,b,t):return tuple(a[i]+(b[i]-a[i])*t for i in range(3))
    def patch(corners,mat,d,u0=.08,u1=.92,v0=.10,v1=.88):
        a,b,c,dv=corners
        def point(u,v):
            p=lerp(lerp(a,b,u),lerp(dv,c,u),v)
            return tuple(p[i]+d[i]*.007 for i in range(3))
        mesh.face([point(u0,v0),point(u1,v0),point(u1,v1),point(u0,v1)],mat,d)
    patch([b[2],b[3],t[3],t[2]],'Glass',(0,0,1))
    patch([b[0],b[1],t[1],t[0]],'Glass',(0,0,-1))
    for i,side in [(1,1),(3,-1)]:
        corners=[b[i],b[(i+1)%4],t[(i+1)%4],t[i]]
        if sedan:
            patch(corners,'Glass',(side,0,0),.06,.47)
            patch(corners,'Glass',(side,0,0),.53,.94)
        else:patch(corners,'Glass',(side,0,0))

def wheel(name,x,z,radius,width):
    mesh=Mesh(name,(x,radius,z));rings=[]
    for xx,r in [(-width/2,radius*.87),(-width*.32,radius),(width*.32,radius),(width/2,radius*.87)]:
        rings.append([(xx,math.cos(i*math.tau/12)*r,math.sin(i*math.tau/12)*r) for i in range(12)])
    for j in range(3):
        for i in range(12):
            points=[rings[j][i],rings[j][(i+1)%12],rings[j+1][(i+1)%12],rings[j+1][i]]
            mesh.face(points,'Tires',(0,sum(p[1] for p in points),sum(p[2] for p in points)))
    for side,ring in [(-1,rings[0]),(1,rings[-1])]:
        mesh.face(ring,'Tires',(side,0,0))
        hub=[(side*(width/2+.006),math.cos(i*math.tau/10)*radius*.53,math.sin(i*math.tau/10)*radius*.53) for i in range(10)]
        mesh.face(hub,'Trim',(side,0,0))
        mesh.box((side*(width/2+.014),0,0),(.025,radius*.35,radius*.35),'DarkTrim')
    return mesh

def vehicle(kind):
    pickup=kind=='Pickup';paint=kind+'_BodyPaint'
    mesh=Mesh('Body');half=2.55 if pickup else 2.30;width=.92 if pickup else .86
    axle=1.65 if pickup else 1.43;top=1.03 if pickup else 1.0
    body(mesh,half,width,(-axle,axle),paint,top)
    if pickup:
        # The bed stays open with a dark ribbed floor and painted sides/tailgate.
        mesh.box((0,1.047,-1.58),(1.52,.035,1.70),'DarkTrim')
        for x in (-.45,-.15,.15,.45):mesh.box((x,1.076,-1.58),(.032,.025,1.60),'Trim')
        for side in (-1,1):mesh.box((side*.85,1.20,-1.55),(.14,.36,1.97),paint)
        mesh.box((0,1.20,-2.48),(1.78,.36,.12),paint)
        mesh.box((0,1.20,-.61),(1.78,.36,.12),paint)
        cabin(mesh,-.60,-.47,.65,1.13,top,1.88,.85,.75,paint,False)
        mesh.box((0,1.215,-2.55),(.31,.07,.028),'Trim')
        mesh.box((0,1.06,1.72),(1.71,.08,1.05),paint)
    else:
        cabin(mesh,-1.16,-.68,.45,1.08,top,1.64,.79,.67,paint,True)
    # Bumpers, grille, rectangular lamps and plates are distinct material slots.
    for end in (-1,1):
        mesh.box((0,.49,end*(half+.045)),(width*2+.08,.14,.14),'Trim')
        mesh.box((0,.56,end*(half+.121)),(.36,.16,.018),'DarkTrim')
    mesh.box((0,.77,half+.012),(width*1.9,.29,.028),'DarkTrim')
    for y in (.69,.77,.85):mesh.box((0,y,half+.032),(.80,.025,.025),'Trim')
    for side in (-1,1):
        mesh.box((side*.64,.78,half+.036),(.31,.21,.04),'Headlights')
        mesh.box((side*.70,.78,-half-.017),(.24,.20,.04),'Taillights')
        mesh.box((side*(width+.012),.80,.32 if pickup else .55),(.025,.045,.16),'Trim')
        if not pickup:mesh.box((side*(width+.012),.80,-.68),(.025,.045,.16),'Trim')
        # Small mirrors and lower door seams.
        mesh.box((side*(width+.11),1.13,.93),(.22,.13,.15),'DarkTrim')
        for z in ((-.55,) if pickup else (-.97,-.02)):
            mesh.box((side*(width+.005),.69,z),(.011,.45,.012),'DarkTrim')
    meshes=[mesh]
    for side,label in [(-1,'Left'),(1,'Right')]:
        for z,axle_name in [(-axle,'Rear'),(axle,'Front')]:
            meshes.append(wheel('Wheel_'+axle_name+'_'+label,side*(width+.025),z,.39 if pickup else .355,.27 if pickup else .235))
    return meshes

def P(name,type,*values):return N('P',[name,type,'','A',*values])
def export(kind,meshes):
    used=list(dict.fromkeys(mat for mesh in meshes for _,mat in mesh.faces))
    mats={name:2000+i for i,name in enumerate(used)}
    objects=[];connections=[]
    objects.append(N('Model',[1000,kind+'\x00\x01Model','Null'],[N('Version',[('I',232)]),N('Properties70',[],[P('Lcl Scaling','Lcl Scaling',1.,1.,1.)])]))
    connections.append(N('C',['OO',1000,0]))
    for name,id in mats.items():
        objects.append(N('Material',[id,name+'\x00\x01Material',''],[N('Version',[('I',102)]),N('ShadingModel',['Phong']),
            N('Properties70',[],[P('DiffuseColor','Color',*COLORS[name]),P('SpecularFactor','Number',.12),P('Shininess','Number',10.)])]))
    for i,mesh in enumerate(meshes):
        model_id=3000+i;geo_id=4000+i
        mesh_mats=list(dict.fromkeys(mat for _,mat in mesh.faces));verts=[];normals=[];indices=[];mat_ids=[]
        for points,mat in mesh.faces:
            n=normal(points)
            indices.extend([len(verts)//3,len(verts)//3+1,-(len(verts)//3+3)])
            verts.extend(t for p in points for t in p);normals.extend(n*3);mat_ids.append(mesh_mats.index(mat))
        layer=[N('Version',[('I',100)])]+[N('LayerElement',[],[N('Type',[t]),N('TypedIndex',[('I',0)])]) for t in ('LayerElementNormal','LayerElementMaterial')]
        objects.append(N('Geometry',[geo_id,mesh.name+'\x00\x01Geometry','Mesh'],[
            N('GeometryVersion',[('I',124)]),N('Vertices',[('d',verts)]),N('PolygonVertexIndex',[('i',indices)]),
            N('LayerElementNormal',[('I',0)],[N('Version',[('I',101)]),N('Name',['']),N('MappingInformationType',['ByPolygonVertex']),N('ReferenceInformationType',['Direct']),N('Normals',[('d',normals)])]),
            N('LayerElementMaterial',[('I',0)],[N('Version',[('I',101)]),N('Name',['']),N('MappingInformationType',['ByPolygon']),N('ReferenceInformationType',['IndexToDirect']),N('Materials',[('i',mat_ids)])]),
            N('Layer',[('I',0)],layer)]))
        objects.append(N('Model',[model_id,mesh.name+'\x00\x01Model','Mesh'],[N('Version',[('I',232)]),
            N('Properties70',[],[P('Lcl Translation','Lcl Translation',*map(float,mesh.position)),P('Lcl Rotation','Lcl Rotation',0.,0.,0.),P('Lcl Scaling','Lcl Scaling',1.,1.,1.)]),
            N('Shading',[('I',1)]),N('Culling',['CullingOff'])]))
        connections.extend([N('C',['OO',geo_id,model_id]),N('C',['OO',model_id,1000])])
        connections.extend(N('C',['OO',mats[m],model_id]) for m in mesh_mats)
    globals=[P('UpAxis','int',('I',1)),P('UpAxisSign','int',('I',1)),P('FrontAxis','int',('I',2)),
        P('FrontAxisSign','int',('I',1)),P('CoordAxis','int',('I',0)),P('CoordAxisSign','int',('I',1)),P('UnitScaleFactor','double',100.)]
    nodes=[N('FBXHeaderExtension',[],[N('FBXHeaderVersion',[('I',1003)]),N('FBXVersion',[('I',7400)]),N('Creator',['Scavenger vehicle builder'])]),
        N('GlobalSettings',[],[N('Version',[('I',1000)]),N('Properties70',[],globals)]),
        N('Documents',[],[N('Count',[('I',1)]),N('Document',[5000,'Scene','Scene'],[N('RootNode',[0])])]),N('References'),
        N('Definitions',[],[N('Version',[('I',100)]),N('Count',[('I',len(objects))])]+[N('ObjectType',[t],[N('Count',[('I',sum(o[0]==t for o in objects))])]) for t in ('Model','Geometry','Material')]),
        N('Objects',[],objects),N('Connections',[],connections),N('Takes',[],[N('Current',[''])])]
    path=OUT/(kind+'.fbx');write(path,nodes)
    loaded=read(path)
    assert loaded==nodes,'FBX round trip failed'
    ids={n[1][0] for n in objects};assert len(ids)==len(objects)
    assert all(c[1][1] in ids and (c[1][2] in ids or c[1][2]==0) for c in connections)
    template=(ROOT/'Assets/Models/dumpster_gray_unity.fbx.meta').read_text()
    template=re.sub(r'(?m)^guid: .*','guid: '+guid(kind),template,count=1)
    mapping='  externalObjects:\n'
    for name in used:mapping+=f'  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: {name}\n    second: {{fileID: 2100000, guid: {guid(name)}, type: 2}}\n'
    template=re.sub(r'  externalObjects:.*?(?=  materials:)',lambda m:mapping,template,flags=re.S)
    path.with_suffix('.fbx.meta').write_text(template)
    print(f'{kind}: {sum(len(m.faces) for m in meshes)} triangles, {len(meshes)} meshes, FBX round-trip and connections verified.')

def materials():
    template=(ROOT/'Assets/Models/VendingMachine/Generated/Steel.mat').read_text().split('--- !u!114')[0]
    for name,color in COLORS.items():
        content=template.replace('m_Name: Steel','m_Name: '+name)
        for prop in ('_BaseColor','_Color'):content=re.sub(r'('+prop+r': )\{[^}]+\}',lambda m:m[1]+'{r: %s, g: %s, b: %s, a: 1}'%color,content)
        content=re.sub(r'(_Metallic: )[^\n]+',lambda m:m[1]+('.55' if name=='Trim' else '0'),content)
        content=re.sub(r'(_Smoothness: )[^\n]+',lambda m:m[1]+('.35' if name=='Glass' else '.18'),content)
        (OUT/(name+'.mat')).write_text(content)
        (OUT/(name+'.mat.meta')).write_text('fileFormatVersion: 2\nguid: '+guid(name)+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n')

if __name__=='__main__':
    materials()
    for kind in ('Pickup','Sedan'):export(kind,vehicle(kind))
