"""Small FBX 7.4 binary reader/writer for the static vehicle asset builder."""
import struct
import zlib

def prop(value):
    if isinstance(value, str):
        b=value.encode();return b'S'+struct.pack('<I',len(b))+b
    if isinstance(value, bytes):return b'R'+struct.pack('<I',len(value))+value
    if isinstance(value, float):return b'D'+struct.pack('<d',value)
    if isinstance(value, int):return b'L'+struct.pack('<q',value)
    kind,values=value
    if kind=='I':return b'I'+struct.pack('<i',values)
    fmt={'d':'d','i':'i','f':'f'}[kind]
    b=struct.pack('<'+fmt*len(values),*values)
    return kind.encode()+struct.pack('<III',len(values),0,len(b))+b

def node(name, values=(), children=()):return [name,list(values),list(children)]
def encode(n,start):
    name=n[0].encode();props=b''.join(prop(p) for p in n[1])
    pos=start+13+len(name)+len(props);children=b''
    for child in n[2]:
        encoded=encode(child,pos);children+=encoded;pos+=len(encoded)
    # FBX empty containers still require an explicit end-of-scope record.
    if n[2] or not n[1]:children+=bytes(13);pos+=13
    return struct.pack('<IIIB',pos,len(n[1]),len(props),len(name))+name+props+children

def write(path,nodes):
    data=b'Kaydara FBX Binary  \x00\x1a\x00'+struct.pack('<I',7400)
    for n in nodes:data+=encode(n,len(data))
    data+=bytes(13)
    data+=bytes.fromhex('fabcab09d0c8d466b176fb831cf7267e')
    data+=bytes((-len(data))%16)+bytes(4)+struct.pack('<I',7400)+bytes(120)
    data+=bytes.fromhex('f85a8c6adef5d97eece90ce3758f290b')
    path.write_bytes(data)

def read(path):
    data=path.read_bytes();version=struct.unpack_from('<I',data,23)[0]
    assert version==7400,version
    def parse(offset):
        end,count,length,nlen=struct.unpack_from('<IIIB',data,offset)
        if not end:return None,offset+13
        offset+=13;name=data[offset:offset+nlen].decode();offset+=nlen;props=[]
        for _ in range(count):
            kind=chr(data[offset]);offset+=1
            if kind in 'S R'.split():
                length=struct.unpack_from('<I',data,offset)[0];offset+=4
                value=data[offset:offset+length];offset+=length
                if kind=='S':value=value.decode()
            elif kind in 'ILDFYC':
                fmt={'I':'i','L':'q','D':'d','F':'f','Y':'h','C':'?'}[kind]
                value=struct.unpack_from('<'+fmt,data,offset)[0];offset+=struct.calcsize(fmt)
                if kind=='I':value=('I',value)
            elif kind in 'diflb':
                length,compressed,size=struct.unpack_from('<III',data,offset);offset+=12
                raw=data[offset:offset+size];offset+=size
                if compressed:raw=zlib.decompress(raw)
                value=(kind,list(struct.unpack('<'+{'d':'d','f':'f','i':'i','l':'q','b':'?'}[kind]*length,raw)))
            else:raise ValueError(kind)
            props.append(value)
        children=[]
        while offset<end:
            child,offset=parse(offset)
            if child is None:break
            children.append(child)
        assert offset==end,(name,offset,end)
        return node(name,props,children),end
    nodes=[];offset=27
    while True:
        n,offset=parse(offset)
        if n is None:break
        nodes.append(n)
    return nodes

if __name__=='__main__':
    from pathlib import Path
    source=Path(__file__).resolve().parents[1]/'Assets/Models/dumpster_gray_unity.fbx'
    nodes=read(source)
    for n in nodes:
        print(n[0],n[1],len(n[2]))
        if n[0]=='Objects':
            for kind in ('Geometry','Model','Material'):
                sample=next(x for x in n[2] if x[0]==kind)
                print(kind,sample[1],[(c[0],c[1] if c[0] not in ('Vertices','PolygonVertexIndex','Edges') else 'array',[(k[0],k[1][:4]) for k in c[2]][:8]) for c in sample[2]])
