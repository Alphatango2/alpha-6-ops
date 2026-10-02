import json,pathlib,ctypes,datetime
root=pathlib.Path(__file__).resolve().parent
current=json.loads((root/'current-run.json').read_text(encoding='utf-8-sig'))
u=ctypes.windll.user32
u.GetClassNameW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_int]
u.GetWindowTextW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_int]
u.GetWindowThreadProcessId.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)]
u.GetClassLongPtrW.argtypes=[ctypes.c_void_p,ctypes.c_int];u.GetClassLongPtrW.restype=ctypes.c_void_p
k=ctypes.windll.kernel32;k.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];k.OpenProcess.restype=ctypes.c_void_p;k.CloseHandle.argtypes=[ctypes.c_void_p]
ps=ctypes.windll.psapi;ps.GetModuleFileNameExW.argtypes=[ctypes.c_void_p,ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_ulong]
results=[]
for p in pathlib.Path(current['directory']).glob('context-*/scheduling-trace.json'):
    events=json.loads(p.read_text())['events']
    for hwnd in set(x['Id'] for x in events if x['Kind']=='native-message' and x['Name']=='0x60'):
        cls=ctypes.create_unicode_buffer(512);title=ctypes.create_unicode_buffer(512);pid=ctypes.c_ulong()
        u.GetClassNameW(hwnd,cls,512);u.GetWindowTextW(hwnd,title,512);tid=u.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
        module=u.GetClassLongPtrW(hwnd,-16);process=k.OpenProcess(0x410,0,pid.value);file=ctypes.create_unicode_buffer(1024)
        if process:
            ps.GetModuleFileNameExW(process,module,file,1024);k.CloseHandle(process)
        results.append(dict(path=str(p),hwnd=hwnd,className=cls.value,title=title.value,pid=pid.value,tid=tid,expectedPid=current['pid'],ownedLive=pid.value==current['pid'],classModule=file.value))
out=root/('native-window-'+datetime.datetime.now().strftime('%H%M%S')+'.json')
out.write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
