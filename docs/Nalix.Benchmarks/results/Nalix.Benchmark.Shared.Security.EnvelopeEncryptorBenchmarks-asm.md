## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.DecryptObject()
       push      rbp
       sub       rsp,20
       lea       rbp,[rsp+20]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rax,[rbp+10]
       mov       r9,[rax+10]
       mov       rax,[rbp+10]
       mov       rdx,[rax+20]
       mov       rcx,7FFF2C1175E8
       call      qword ptr [7FFF2C09CD98]; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       nop
       add       rsp,20
       pop       rbp
       ret
; Total bytes of code 61
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       push      rbp
       sub       rsp,0E0
       lea       rbp,[rsp+0E0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       mov       rcx,[rbp+18]
       mov       rdx,1B9923B3580
       call      qword ptr [7FFF2BE7FB88]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,1B9923B4CD0
       call      qword ptr [7FFF2BE7FB88]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+28]
       mov       [rbp-50],rax
       cmp       qword ptr [rbp+28],0
       jne       short M01_L00
       mov       rax,1B9D3C01318
       mov       rax,[rax]
       mov       [rbp-50],rax
M01_L00:
       mov       rax,[rbp-50]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB15860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-70],rax
       mov       rdx,[rbp-70]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BE7FBA0]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
M01_L01:
       lea       r8,[rbp-30]
       mov       rax,1B9D3C01320
       mov       rcx,[rax]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2C09F540]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       jne       short M01_L02
       mov       rcx,offset MT_System.Collections.Generic.Dictionary<System.String, Nalix.Shared.Security.Internal.EncryptedValueStorage>
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-68],rax
       mov       rcx,[rbp-68]
       call      qword ptr [7FFF2BDE7138]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor()
       mov       rax,[rbp-68]
       mov       [rbp-30],rax
M01_L02:
       xor       eax,eax
       mov       [rbp-38],eax
       mov       eax,[rbp-18]
       mov       [rbp-3C],eax
       mov       rax,[rbp-30]
       mov       [rbp-58],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB15860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-78],rax
       mov       rax,[rbp-78]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-20]
       mov       r8,[rbp-58]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       mov       dword ptr [rsp+38],1
       call      qword ptr [7FFF2C09F558]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rax,[rbp-30]
       mov       [rbp-60],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB15860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-80],rax
       mov       rax,[rbp-80]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-28]
       mov       r8,[rbp-60]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       xor       eax,eax
       mov       [rsp+38],eax
       call      qword ptr [7FFF2C09F558]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rcx,[rbp-30]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BB5ABE0]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].Clear()
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB1F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-48],rax
       cmp       qword ptr [rbp-48],0
       je        short M01_L03
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA608C8
       call      qword ptr [r11]
       mov       [rbp-84],eax
       mov       ecx,[rbp-84]
       mov       edx,4
       call      qword ptr [7FFF2C09F528]
       mov       [rbp-88],eax
       mov       edx,[rbp-88]
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA608D0
       call      qword ptr [r11]
M01_L03:
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
; Total bytes of code 657
```

## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.EncryptObject()
       push      rbp
       sub       rsp,30
       lea       rbp,[rsp+30]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       rax,[rax+10]
       mov       [rsp+20],rax
       mov       rax,[rbp+10]
       movzx     r9d,byte ptr [rax+28]
       mov       rax,[rbp+10]
       mov       rdx,[rax+18]
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rcx,7FFF2BED80A0
       call      qword ptr [7FFF2BE7FC18]; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       nop
       add       rsp,30
       pop       rbp
       ret
; Total bytes of code 75
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       push      rbp
       sub       rsp,90
       lea       rbp,[rsp+90]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9d
       mov       rcx,[rbp+18]
       mov       rdx,1A679913580
       call      qword ptr [7FFF2BE7FC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,1A679914CD0
       call      qword ptr [7FFF2BE7FC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+30]
       mov       [rbp-40],rax
       cmp       qword ptr [rbp+30],0
       jne       short M01_L00
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB15728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,19E8E801318
       mov       rax,[rax]
       mov       [rbp-40],rax
M01_L00:
       mov       rax,[rbp-40]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB15860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-48],rax
       mov       rdx,[rbp-48]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BE7FC78]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
M01_L01:
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB15728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,19E8E801320
       mov       rax,[rax]
       mov       [rbp-50],rax
       mov       rcx,[rbp-50]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BE7FC90]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetOrCreateValue(System.__Canon)
       mov       [rbp-30],rax
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-20]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BE7FCA8]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-28]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BE7FCA8]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB1F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-38],rax
       cmp       qword ptr [rbp-38],0
       je        short M01_L02
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA603F0
       call      qword ptr [r11]
       mov       [rbp-54],eax
       mov       ecx,[rbp-54]
       mov       edx,4
       call      qword ptr [7FFF2BE7FC48]
       mov       [rbp-58],eax
       mov       edx,[rbp-58]
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA603F8
       call      qword ptr [r11]
M01_L02:
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
; Total bytes of code 440
```

## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.DecryptObject()
       push      rbp
       sub       rsp,20
       lea       rbp,[rsp+20]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rax,[rbp+10]
       mov       r9,[rax+10]
       mov       rax,[rbp+10]
       mov       rdx,[rax+20]
       mov       rcx,7FFF2C128720
       call      qword ptr [7FFF2C09D410]; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       nop
       add       rsp,20
       pop       rbp
       ret
; Total bytes of code 61
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       push      rbp
       sub       rsp,0E0
       lea       rbp,[rsp+0E0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       mov       rcx,[rbp+18]
       mov       rdx,29C124B3580
       call      qword ptr [7FFF2BE7FC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,29C124B4CD0
       call      qword ptr [7FFF2BE7FC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+28]
       mov       [rbp-50],rax
       cmp       qword ptr [rbp+28],0
       jne       short M01_L00
       mov       rax,29C28401318
       mov       rax,[rax]
       mov       [rbp-50],rax
M01_L00:
       mov       rax,[rbp-50]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB15860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-70],rax
       mov       rdx,[rbp-70]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BE7FC78]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
M01_L01:
       lea       r8,[rbp-30]
       mov       rax,29C28401320
       mov       rcx,[rax]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2C09FBA0]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       jne       short M01_L02
       mov       rcx,offset MT_System.Collections.Generic.Dictionary<System.String, Nalix.Shared.Security.Internal.EncryptedValueStorage>
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-68],rax
       mov       rcx,[rbp-68]
       call      qword ptr [7FFF2BDE7138]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor()
       mov       rax,[rbp-68]
       mov       [rbp-30],rax
M01_L02:
       xor       eax,eax
       mov       [rbp-38],eax
       mov       eax,[rbp-18]
       mov       [rbp-3C],eax
       mov       rax,[rbp-30]
       mov       [rbp-58],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB15860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-78],rax
       mov       rax,[rbp-78]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-20]
       mov       r8,[rbp-58]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       mov       dword ptr [rsp+38],1
       call      qword ptr [7FFF2C09FBB8]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rax,[rbp-30]
       mov       [rbp-60],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB15860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-80],rax
       mov       rax,[rbp-80]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-28]
       mov       r8,[rbp-60]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       xor       eax,eax
       mov       [rsp+38],eax
       call      qword ptr [7FFF2C09FBB8]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rcx,[rbp-30]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BB5ABE0]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].Clear()
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB1F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-48],rax
       cmp       qword ptr [rbp-48],0
       je        short M01_L03
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA608C8
       call      qword ptr [r11]
       mov       [rbp-84],eax
       mov       ecx,[rbp-84]
       mov       edx,4
       call      qword ptr [7FFF2C09FB88]
       mov       [rbp-88],eax
       mov       edx,[rbp-88]
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA608D0
       call      qword ptr [r11]
M01_L03:
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
; Total bytes of code 657
```

## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.DecryptObject()
       push      rbp
       sub       rsp,20
       lea       rbp,[rsp+20]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rax,[rbp+10]
       mov       r9,[rax+10]
       mov       rax,[rbp+10]
       mov       rdx,[rax+20]
       mov       rcx,7FFF2C147D70
       call      qword ptr [7FFF2C0CCF78]; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       nop
       add       rsp,20
       pop       rbp
       ret
; Total bytes of code 61
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       push      rbp
       sub       rsp,0E0
       lea       rbp,[rsp+0E0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       mov       rcx,[rbp+18]
       mov       rdx,228923B3580
       call      qword ptr [7FFF2BEAFB88]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,228923B4CD0
       call      qword ptr [7FFF2BEAFB88]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+28]
       mov       [rbp-50],rax
       cmp       qword ptr [rbp+28],0
       jne       short M01_L00
       mov       rax,228CF801318
       mov       rax,[rax]
       mov       [rbp-50],rax
M01_L00:
       mov       rax,[rbp-50]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB45860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-70],rax
       mov       rdx,[rbp-70]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BEAFBA0]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
M01_L01:
       lea       r8,[rbp-30]
       mov       rax,228CF801320
       mov       rcx,[rax]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2C0CF6F0]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       jne       short M01_L02
       mov       rcx,offset MT_System.Collections.Generic.Dictionary<System.String, Nalix.Shared.Security.Internal.EncryptedValueStorage>
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-68],rax
       mov       rcx,[rbp-68]
       call      qword ptr [7FFF2BE17138]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor()
       mov       rax,[rbp-68]
       mov       [rbp-30],rax
M01_L02:
       xor       eax,eax
       mov       [rbp-38],eax
       mov       eax,[rbp-18]
       mov       [rbp-3C],eax
       mov       rax,[rbp-30]
       mov       [rbp-58],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB45860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-78],rax
       mov       rax,[rbp-78]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-20]
       mov       r8,[rbp-58]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       mov       dword ptr [rsp+38],1
       call      qword ptr [7FFF2C0CF708]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rax,[rbp-30]
       mov       [rbp-60],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB45860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-80],rax
       mov       rax,[rbp-80]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-28]
       mov       r8,[rbp-60]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       xor       eax,eax
       mov       [rsp+38],eax
       call      qword ptr [7FFF2C0CF708]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rcx,[rbp-30]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BB8ABE0]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].Clear()
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB4F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-48],rax
       cmp       qword ptr [rbp-48],0
       je        short M01_L03
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA908C8
       call      qword ptr [r11]
       mov       [rbp-84],eax
       mov       ecx,[rbp-84]
       mov       edx,4
       call      qword ptr [7FFF2C0CF6D8]
       mov       [rbp-88],eax
       mov       edx,[rbp-88]
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA908D0
       call      qword ptr [r11]
M01_L03:
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
; Total bytes of code 657
```

## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.EncryptObject()
       push      rbp
       sub       rsp,30
       lea       rbp,[rsp+30]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       rax,[rax+10]
       mov       [rsp+20],rax
       mov       rax,[rbp+10]
       movzx     r9d,byte ptr [rax+28]
       mov       rax,[rbp+10]
       mov       rdx,[rax+18]
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rcx,7FFF2BF080A0
       call      qword ptr [7FFF2BEAFC18]; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       nop
       add       rsp,30
       pop       rbp
       ret
; Total bytes of code 75
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       push      rbp
       sub       rsp,90
       lea       rbp,[rsp+90]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9d
       mov       rcx,[rbp+18]
       mov       rdx,245923B3580
       call      qword ptr [7FFF2BEAFC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,245923B4CD0
       call      qword ptr [7FFF2BEAFC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+30]
       mov       [rbp-40],rax
       cmp       qword ptr [rbp+30],0
       jne       short M01_L00
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB45728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,245EB401318
       mov       rax,[rax]
       mov       [rbp-40],rax
M01_L00:
       mov       rax,[rbp-40]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB45860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-48],rax
       mov       rdx,[rbp-48]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BEAFC78]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
M01_L01:
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB45728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,245EB401320
       mov       rax,[rax]
       mov       [rbp-50],rax
       mov       rcx,[rbp-50]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BEAFC90]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetOrCreateValue(System.__Canon)
       mov       [rbp-30],rax
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-20]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BEAFCA8]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-28]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BEAFCA8]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB4F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-38],rax
       cmp       qword ptr [rbp-38],0
       je        short M01_L02
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA903F0
       call      qword ptr [r11]
       mov       [rbp-54],eax
       mov       ecx,[rbp-54]
       mov       edx,4
       call      qword ptr [7FFF2BEAFC48]
       mov       [rbp-58],eax
       mov       edx,[rbp-58]
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA903F8
       call      qword ptr [r11]
M01_L02:
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
; Total bytes of code 440
```

## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.EncryptObject()
       push      rbp
       sub       rsp,30
       lea       rbp,[rsp+30]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       rax,[rax+10]
       mov       [rsp+20],rax
       mov       rax,[rbp+10]
       movzx     r9d,byte ptr [rax+28]
       mov       rax,[rbp+10]
       mov       rdx,[rax+18]
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rcx,7FFF2BF080A0
       call      qword ptr [7FFF2BEAFC18]; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       nop
       add       rsp,30
       pop       rbp
       ret
; Total bytes of code 75
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       push      rbp
       sub       rsp,90
       lea       rbp,[rsp+90]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9d
       mov       rcx,[rbp+18]
       mov       rdx,203924B3580
       call      qword ptr [7FFF2BEAFC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,203924B4CD0
       call      qword ptr [7FFF2BEAFC60]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+30]
       mov       [rbp-40],rax
       cmp       qword ptr [rbp+30],0
       jne       short M01_L00
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB45728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,203FA001318
       mov       rax,[rax]
       mov       [rbp-40],rax
M01_L00:
       mov       rax,[rbp-40]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB45860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-48],rax
       mov       rdx,[rbp-48]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BEAFC78]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
M01_L01:
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB45728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,203FA001320
       mov       rax,[rax]
       mov       [rbp-50],rax
       mov       rcx,[rbp-50]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BEAFC90]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetOrCreateValue(System.__Canon)
       mov       [rbp-30],rax
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-20]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BEAFCA8]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-28]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BEAFCA8]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB4F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-38],rax
       cmp       qword ptr [rbp-38],0
       je        short M01_L02
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA903F0
       call      qword ptr [r11]
       mov       [rbp-54],eax
       mov       ecx,[rbp-54]
       mov       edx,4
       call      qword ptr [7FFF2BEAFC48]
       mov       [rbp-58],eax
       mov       edx,[rbp-58]
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA903F8
       call      qword ptr [r11]
M01_L02:
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
; Total bytes of code 440
```

## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.DecryptObject()
       push      rbp
       sub       rsp,20
       lea       rbp,[rsp+20]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rax,[rbp+10]
       mov       r9,[rax+10]
       mov       rax,[rbp+10]
       mov       rdx,[rax+20]
       mov       rcx,7FFF2C14A180
       call      qword ptr [7FFF2C0BD440]; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       nop
       add       rsp,20
       pop       rbp
       ret
; Total bytes of code 61
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Decrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Byte[])
       push      rbp
       sub       rsp,0E0
       lea       rbp,[rsp+0E0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       mov       rcx,[rbp+18]
       mov       rdx,15B923B3580
       call      qword ptr [7FFF2BE9FB88]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,15B923B4CD0
       call      qword ptr [7FFF2BE9FB88]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+28]
       mov       [rbp-50],rax
       cmp       qword ptr [rbp+28],0
       jne       short M01_L00
       mov       rax,15BF5C01318
       mov       rax,[rax]
       mov       [rbp-50],rax
M01_L00:
       mov       rax,[rbp-50]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB35860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-70],rax
       mov       rdx,[rbp-70]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BE9FBA0]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
M01_L01:
       lea       r8,[rbp-30]
       mov       rax,15BF5C01320
       mov       rcx,[rax]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2C0BFBB8]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       jne       short M01_L02
       mov       rcx,offset MT_System.Collections.Generic.Dictionary<System.String, Nalix.Shared.Security.Internal.EncryptedValueStorage>
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-68],rax
       mov       rcx,[rbp-68]
       call      qword ptr [7FFF2BE07138]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor()
       mov       rax,[rbp-68]
       mov       [rbp-30],rax
M01_L02:
       xor       eax,eax
       mov       [rbp-38],eax
       mov       eax,[rbp-18]
       mov       [rbp-3C],eax
       mov       rax,[rbp-30]
       mov       [rbp-58],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB35860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-78],rax
       mov       rax,[rbp-78]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-20]
       mov       r8,[rbp-58]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       mov       dword ptr [rsp+38],1
       call      qword ptr [7FFF2C0BFBD0]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rax,[rbp-30]
       mov       [rbp-60],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB35860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-90],rax
       mov       rcx,[rbp-90]
       mov       rax,[rbp-90]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       [rbp-80],rax
       mov       rax,[rbp-80]
       mov       [rsp+40],rax
       lea       rax,[rbp-38]
       mov       [rsp+28],rax
       mov       rdx,[rbp-28]
       mov       r8,[rbp-60]
       mov       r9,[rbp+20]
       mov       rax,[rbp-10]
       mov       [rsp+20],rax
       mov       eax,[rbp-3C]
       mov       [rsp+30],eax
       mov       rcx,[rbp+18]
       xor       eax,eax
       mov       [rsp+38],eax
       call      qword ptr [7FFF2C0BFBD0]; Nalix.Shared.Security.EnvelopeEncryptor.DecryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Byte[], Int32 ByRef, Int32, Boolean, System.String)
       mov       rcx,[rbp-30]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BB7ABE0]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].Clear()
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB3F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-48],rax
       cmp       qword ptr [rbp-48],0
       je        short M01_L03
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA808C8
       call      qword ptr [r11]
       mov       [rbp-84],eax
       mov       ecx,[rbp-84]
       mov       edx,4
       call      qword ptr [7FFF2C0BFBA0]
       mov       [rbp-88],eax
       mov       edx,[rbp-88]
       mov       rcx,[rbp-48]
       mov       r11,7FFF2BA808D0
       call      qword ptr [r11]
M01_L03:
       mov       rax,[rbp+18]
       add       rsp,0E0
       pop       rbp
       ret
; Total bytes of code 657
```

## .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0, InvocationCount=1, UnrollFactor=1))

```assembly
; Nalix.Benchmark.Shared.Security.EnvelopeEncryptorBenchmarks.EncryptObject()
       push      rbp
       sub       rsp,30
       lea       rbp,[rsp+30]
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       rax,[rax+10]
       mov       [rsp+20],rax
       mov       rax,[rbp+10]
       movzx     r9d,byte ptr [rax+28]
       mov       rax,[rbp+10]
       mov       rdx,[rax+18]
       mov       rax,[rbp+10]
       mov       r8,[rax+8]
       mov       rcx,7FFF2BEF8090
       call      qword ptr [7FFF2BE9FC00]; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       nop
       add       rsp,30
       pop       rbp
       ret
; Total bytes of code 75
```
```assembly
; Nalix.Shared.Security.EnvelopeEncryptor.Encrypt[[System.__Canon, System.Private.CoreLib]](System.__Canon, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       push      rbp
       sub       rsp,90
       lea       rbp,[rsp+90]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqu   ymmword ptr [rbp-30],ymm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp-8],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9d
       mov       rcx,[rbp+18]
       mov       rdx,16A924B3580
       call      qword ptr [7FFF2BE9FC48]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rcx,[rbp+20]
       mov       rdx,16A924B4CD0
       call      qword ptr [7FFF2BE9FC48]; System.ArgumentNullException.ThrowIfNull(System.Object, System.String)
       mov       rax,[rbp+30]
       mov       [rbp-40],rax
       cmp       qword ptr [rbp+30],0
       jne       short M01_L00
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB35728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,16AA2001318
       mov       rax,[rax]
       mov       [rbp-40],rax
M01_L00:
       mov       rax,[rbp-40]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       mov       rax,[rax+18]
       mov       rcx,[rax]
       call      qword ptr [7FFF2BB35860]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       [rbp-48],rax
       mov       rdx,[rbp-48]
       lea       rcx,[rbp-28]
       call      qword ptr [7FFF2BE9FC60]; Nalix.Shared.Security.Internal.EnvelopeMemberResolver.GetMembers(System.Type)
       movzx     eax,byte ptr [rbp-14]
       test      eax,eax
       jne       short M01_L01
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
M01_L01:
       mov       rcx,offset MT_Nalix.Shared.Security.EnvelopeEncryptor
       call      qword ptr [7FFF2BB35728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,16AA2001320
       mov       rax,[rax]
       mov       [rbp-50],rax
       mov       rcx,[rbp-50]
       mov       rdx,[rbp+18]
       cmp       [rcx],ecx
       call      qword ptr [7FFF2BE9FC78]; System.Runtime.CompilerServices.ConditionalWeakTable`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetOrCreateValue(System.__Canon)
       mov       [rbp-30],rax
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-20]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BE9FC90]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       movzx     eax,byte ptr [rbp+28]
       mov       [rsp+20],eax
       mov       rax,[rbp-10]
       mov       [rsp+28],rax
       mov       rcx,[rbp+18]
       mov       rdx,[rbp-28]
       mov       r8,[rbp-30]
       mov       r9,[rbp+20]
       call      qword ptr [7FFF2BE9FC90]; Nalix.Shared.Security.EnvelopeEncryptor.EncryptMembers(System.Object, Nalix.Shared.Security.Internal.SensitiveMemberInfo[], System.Collections.Generic.Dictionary`2<System.String,Nalix.Shared.Security.Internal.EncryptedValueStorage>, Byte[], Nalix.Common.Security.Enums.CipherSuiteType, Byte[])
       mov       rdx,[rbp+18]
       mov       rcx,offset MT_Nalix.Common.Networking.Packets.Abstractions.IPacket
       call      qword ptr [7FFF2BB3F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       [rbp-38],rax
       cmp       qword ptr [rbp-38],0
       je        short M01_L02
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA803F0
       call      qword ptr [r11]
       mov       [rbp-54],eax
       mov       ecx,[rbp-54]
       mov       edx,4
       call      qword ptr [7FFF2BE9FC30]
       mov       [rbp-58],eax
       mov       edx,[rbp-58]
       mov       rcx,[rbp-38]
       mov       r11,7FFF2BA803F8
       call      qword ptr [r11]
M01_L02:
       mov       rax,[rbp+18]
       add       rsp,90
       pop       rbp
       ret
; Total bytes of code 440
```

