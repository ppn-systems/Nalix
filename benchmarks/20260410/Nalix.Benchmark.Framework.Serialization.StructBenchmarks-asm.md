## .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0))

```assembly
; Nalix.Benchmark.Framework.Serialization.StructBenchmarks.Serialize_IntoSpan()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0F0
       lea       rbp,[rsp+110]
       vxorps    xmm4,xmm4,xmm4
       vmovdqa   xmmword ptr [rbp-0F0],xmm4
       mov       rax,0FFFFFFFFFFFFFF40
M00_L00:
       vmovdqa   xmmword ptr [rbp+rax-20],xmm4
       vmovdqa   xmmword ptr [rbp+rax-10],xmm4
       vmovdqa   xmmword ptr [rax+rbp],xmm4
       add       rax,30
       jne       short M00_L00
       mov       rbx,[rcx+10]
       lea       rsi,[rcx+18]
       mov       rdi,[rcx+8]
       test      rdi,rdi
       je        short M00_L02
       cmp       byte ptr [7FF8B904B0FA],0
       jne       short M00_L03
       movzx     ecx,byte ptr [7FF8B904B0F9]
       test      ecx,ecx
       jne       short M00_L03
       cmp       dword ptr [rdi+8],38
       jl        short M00_L04
       add       rdi,10
       call      CORINFO_HELP_ASSIGN_BYREF
       call      CORINFO_HELP_ASSIGN_BYREF
       mov       ecx,5
       rep movsq
       mov       esi,38
M00_L01:
       mov       [rbx+38],esi
       vzeroupper
       add       rsp,0F0
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       ecx,2850
       mov       rdx,7FF8B94D7A58
       call      qword ptr [7FF8B910F210]
       mov       rcx,rax
       call      qword ptr [7FF8B9505E78]
       int       3
M00_L03:
       cmp       byte ptr [7FF8B904B0FB],0
       je        near ptr M00_L10
       jmp       near ptr M00_L09
M00_L04:
       lea       rcx,[rbp-0B8]
       mov       edx,26
       mov       r8d,2
       call      qword ptr [7FF8B910C270]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       mov       ecx,[rbp-0A8]
       cmp       ecx,[rbp-98]
       ja        near ptr M00_L23
       mov       rdx,[rbp-0A0]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-98]
       sub       eax,ecx
       cmp       eax,1C
       jb        short M00_L05
       vmovups   ymm0,[7FF8B9179B80]
       vmovups   [rdx],ymm0
       vmovups   xmm0,[7FF8B9179BA0]
       vmovups   [rdx+20],xmm0
       mov       rcx,20003A00640065
       mov       [rdx+30],rcx
       mov       ecx,[rbp-0A8]
       add       ecx,1C
       mov       [rbp-0A8],ecx
       jmp       short M00_L06
M00_L05:
       lea       rcx,[rbp-0B8]
       mov       edx,15188D10
       call      qword ptr [7FF8B9475EC0]
M00_L06:
       lea       rcx,[rbp-0B8]
       mov       edx,38
       call      qword ptr [7FF8B9107FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       ecx,[rbp-0A8]
       cmp       ecx,[rbp-98]
       ja        near ptr M00_L23
       mov       rdx,[rbp-0A0]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-98]
       sub       eax,ecx
       cmp       eax,0A
       jb        short M00_L07
       vmovups   xmm0,[7FF8B9179BB0]
       vmovups   [rdx],xmm0
       mov       dword ptr [rdx+10],20003A
       mov       ecx,[rbp-0A8]
       add       ecx,0A
       mov       [rbp-0A8],ecx
       jmp       short M00_L08
M00_L07:
       lea       rcx,[rbp-0B8]
       mov       edx,15188D60
       call      qword ptr [7FF8B9475EC0]
M00_L08:
       mov       edx,[rdi+8]
       lea       rcx,[rbp-0B8]
       call      qword ptr [7FF8B9107FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       lea       rcx,[rbp-0B8]
       call      qword ptr [7FF8B910C2A0]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       mov       rsi,rax
       mov       rcx,rdi
       call      qword ptr [7FF8B9505EA8]
       lea       rcx,[rdi+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rdi
       call      CORINFO_HELP_THROW
       int       3
M00_L09:
       mov       r14d,[7FF8B904B0F4]
       mov       ecx,1
       jmp       short M00_L12
M00_L10:
       cmp       byte ptr [7FF8B904B0FC],0
       je        short M00_L11
       mov       r14d,[7FF8B904B0F0]
       mov       ecx,2
       jmp       short M00_L12
M00_L11:
       xor       r14d,r14d
       xor       ecx,ecx
M00_L12:
       cmp       ecx,2
       jne       near ptr M00_L20
       cmp       [rdi+8],r14d
       jge       near ptr M00_L17
       lea       rcx,[rbp-90]
       mov       edx,26
       mov       r8d,2
       call      qword ptr [7FF8B910C270]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       mov       ecx,[rbp-80]
       cmp       ecx,[rbp-70]
       ja        near ptr M00_L23
       mov       rdx,[rbp-78]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-70]
       sub       eax,ecx
       cmp       eax,1C
       jb        short M00_L13
       vmovups   ymm0,[7FF8B9179B80]
       vmovups   [rdx],ymm0
       vmovups   xmm0,[7FF8B9179BA0]
       vmovups   [rdx+20],xmm0
       mov       rcx,20003A00640065
       mov       [rdx+30],rcx
       mov       ecx,[rbp-80]
       add       ecx,1C
       mov       [rbp-80],ecx
       jmp       short M00_L14
M00_L13:
       lea       rcx,[rbp-90]
       mov       edx,15188D10
       call      qword ptr [7FF8B9475EC0]
M00_L14:
       lea       rcx,[rbp-90]
       mov       edx,r14d
       call      qword ptr [7FF8B9107FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       ecx,[rbp-80]
       cmp       ecx,[rbp-70]
       ja        near ptr M00_L23
       mov       rdx,[rbp-78]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-70]
       sub       eax,ecx
       cmp       eax,0A
       jb        short M00_L15
       vmovups   xmm0,[7FF8B9179BB0]
       vmovups   [rdx],xmm0
       mov       dword ptr [rdx+10],20003A
       mov       ecx,[rbp-80]
       add       ecx,0A
       mov       [rbp-80],ecx
       jmp       short M00_L16
M00_L15:
       lea       rcx,[rbp-90]
       mov       edx,15188D60
       call      qword ptr [7FF8B9475EC0]
M00_L16:
       mov       edx,[rdi+8]
       lea       rcx,[rbp-90]
       call      qword ptr [7FF8B9107FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF8B910C2A0]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       mov       rsi,rax
       mov       rcx,rbx
       call      qword ptr [7FF8B9505EA8]
       lea       rcx,[rbx+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rbx
       call      CORINFO_HELP_THROW
       int       3
M00_L17:
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       ecx,80001330
       mov       r14,[rcx]
       lea       rcx,[rbp-68]
       mov       rdx,rdi
       call      qword ptr [7FF8B947FD20]
       nop
       vmovdqu   ymm0,ymmword ptr [rsi]
       vmovdqu   ymmword ptr [rbp-0F0],ymm0
       vmovdqu   ymm0,ymmword ptr [rsi+18]
       vmovdqu   ymmword ptr [rbp-0D8],ymm0
       lea       r8,[rbp-0F0]
       lea       rdx,[rbp-68]
       mov       rcx,r14
       mov       r11,7FF8B90504C8
       call      qword ptr [r11]
       mov       esi,[rbp-60]
       cmp       qword ptr [rbp-68],0
       je        short M00_L19
       cmp       byte ptr [rbp-5C],0
       je        short M00_L18
       mov       rcx,[rbp-68]
       mov       rdi,rcx
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       edx,80001340
       mov       rax,[rdx]
       mov       rdx,rdi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M00_L18:
       xor       ecx,ecx
       mov       [rbp-68],rcx
M00_L19:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-58],xmm0
       xor       ecx,ecx
       mov       [rbp-60],ecx
       jmp       near ptr M00_L01
M00_L20:
       lea       rcx,[rbp-48]
       mov       edx,63
       mov       r8d,1
       call      qword ptr [7FF8B910C270]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       mov       ecx,[rbp-38]
       cmp       ecx,[rbp-28]
       ja        near ptr M00_L23
       mov       rdx,[rbp-30]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-28]
       sub       eax,ecx
       cmp       eax,34
       jb        short M00_L21
       vmovups   ymm0,[7FF8B9179BC0]
       vmovups   [rdx],ymm0
       vmovups   ymm0,[7FF8B9179BE0]
       vmovups   [rdx+20],ymm0
       vmovups   ymm0,[7FF8B9179C00]
       vmovups   [rdx+40],ymm0
       mov       rcx,20006500700079
       mov       [rdx+60],rcx
       mov       ecx,[rbp-38]
       add       ecx,34
       mov       [rbp-38],ecx
       jmp       short M00_L22
M00_L21:
       lea       rcx,[rbp-48]
       mov       edx,15188C90
       call      qword ptr [7FF8B9475EC0]
M00_L22:
       lea       rcx,[rbp-48]
       mov       rdx,7FF8B94D8A58
       mov       r8d,15188B08
       call      qword ptr [7FF8B92EEA18]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.__Canon, System.Private.CoreLib]](System.__Canon)
       mov       ecx,[rbp-38]
       cmp       ecx,[rbp-28]
       jbe       short M00_L24
M00_L23:
       call      qword ptr [7FF8B92E7D50]
       int       3
M00_L24:
       mov       rdx,[rbp-30]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-28]
       sub       eax,ecx
       cmp       eax,2F
       jb        short M00_L25
       vmovups   ymm0,[7FF8B9179C20]
       vmovups   [rdx],ymm0
       vmovups   ymm0,[7FF8B9179C40]
       vmovups   [rdx+20],ymm0
       vmovups   xmm0,[7FF8B9179C60]
       vmovups   [rdx+40],xmm0
       mov       rcx,6500740073006E
       mov       [rdx+50],rcx
       mov       dword ptr [rdx+58],640061
       mov       word ptr [rdx+5C],2E
       mov       ecx,[rbp-38]
       add       ecx,2F
       mov       [rbp-38],ecx
       jmp       short M00_L26
M00_L25:
       lea       rcx,[rbp-48]
       mov       edx,15188D90
       call      qword ptr [7FF8B9475EC0]
M00_L26:
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rcx,[rbp-48]
       call      qword ptr [7FF8B910C2A0]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       mov       rsi,rax
       mov       rcx,rbx
       call      qword ptr [7FF8B9505EA8]
       lea       rcx,[rbx+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rbx
       call      CORINFO_HELP_THROW
       int       3
       sub       rsp,28
       mov       rsi,[rbp-68]
       test      rsi,rsi
       je        short M00_L29
       cmp       byte ptr [rbp-5C],0
       je        short M00_L28
       test      byte ptr [7FF8B94DFA80],1
       jne       short M00_L27
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
M00_L27:
       mov       edx,80001340
       mov       rax,[rdx]
       mov       rdx,rsi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M00_L28:
       xor       ecx,ecx
       mov       [rbp-68],rcx
M00_L29:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-58],xmm0
       xor       ecx,ecx
       mov       [rbp-60],ecx
       vzeroupper
       add       rsp,28
       ret
; Total bytes of code 1534
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       rbx,rcx
       mov       esi,edx
       mov       edi,r8d
       xor       eax,eax
       mov       [rbx],rax
       call      qword ptr [7FF8F43A9080]
       mov       rcx,[rax]
       imul      edx,edi,0B
       add       edx,esi
       mov       eax,100
       cmp       edx,100
       cmovle    edx,eax
       cmp       [rcx],ecx
       call      qword ptr [7FF8F43C8878]; Precode of System.Buffers.SharedArrayPool`1[[System.Char, System.Private.CoreLib]].Rent(Int32)
       mov       [rbx+8],rax
       test      rax,rax
       je        short M01_L01
       lea       rcx,[rax+10]
       mov       eax,[rax+8]
M01_L00:
       mov       [rbx+18],rcx
       mov       [rbx+20],eax
       xor       eax,eax
       mov       [rbx+10],eax
       mov       byte ptr [rbx+14],0
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M01_L01:
       xor       ecx,ecx
       xor       eax,eax
       jmp       short M01_L00
; Total bytes of code 102
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,58
       xorps     xmm4,xmm4
       movaps    [rsp+30],xmm4
       movaps    [rsp+40],xmm4
       mov       rbx,rcx
       mov       esi,edx
       cmp       byte ptr [rbx+14],0
       jne       short M02_L03
M02_L00:
       lea       rdx,[rbx+18]
       mov       r8d,[rbx+10]
       mov       edi,[rdx+8]
       cmp       r8d,edi
       ja        near ptr M02_L10
       mov       rdx,[rdx]
       mov       ecx,r8d
       lea       rbp,[rdx+rcx*2]
       sub       edi,r8d
       mov       rcx,[rbx]
       test      esi,esi
       jl        short M02_L05
       mov       [rsp+40],rbp
       mov       [rsp+48],edi
       lea       rdx,[rsp+40]
       lea       r8,[rsp+50]
       mov       ecx,esi
       call      qword ptr [7FF8F43CFCA8]; Precode of System.Number.TryUInt32ToDecStr[[System.Char, System.Private.CoreLib]](UInt32, System.Span`1<Char>, Int32 ByRef)
M02_L01:
       test      eax,eax
       jne       short M02_L02
       mov       rcx,rbx
       call      qword ptr [7FF8F43C3FC0]
       jmp       short M02_L00
M02_L02:
       mov       eax,[rsp+50]
       add       [rbx+10],eax
       jmp       short M02_L04
M02_L03:
       mov       rcx,rbx
       mov       edx,esi
       xor       r8d,r8d
       call      qword ptr [7FF8F43D0B00]
M02_L04:
       nop
       add       rsp,58
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M02_L05:
       test      rcx,rcx
       je        short M02_L06
       call      qword ptr [7FF8F43C0238]; Precode of System.Globalization.NumberFormatInfo.<GetInstance>g__GetProviderNonNull|58_0(System.IFormatProvider)
       jmp       short M02_L07
M02_L06:
       call      qword ptr [7FF8F43C0220]; Precode of System.Globalization.NumberFormatInfo.get_CurrentInfo()
M02_L07:
       mov       r8,[rax+28]
       test      r8,r8
       jne       short M02_L08
       xor       r9d,r9d
       xor       r8d,r8d
       jmp       short M02_L09
M02_L08:
       lea       r9,[r8+0C]
       mov       r8d,[r8+8]
M02_L09:
       mov       [rsp+30],r9
       mov       [rsp+38],r8d
       mov       [rsp+40],rbp
       mov       [rsp+48],edi
       lea       r8,[rsp+50]
       mov       [rsp+20],r8
       lea       r8,[rsp+30]
       lea       r9,[rsp+40]
       mov       ecx,esi
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF8F43CFC90]
       jmp       near ptr M02_L01
M02_L10:
       call      qword ptr [7FF8F43BE290]
       int       3
; Total bytes of code 255
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rbx,rcx
       lea       rsi,[rbx+18]
       mov       rcx,rsi
       mov       eax,[rbx+10]
       cmp       eax,[rcx+8]
       ja        short M03_L01
       mov       rcx,[rcx]
       mov       [rsp+28],rcx
       mov       [rsp+30],eax
       lea       rcx,[rsp+28]
       call      qword ptr [7FF8F43BAB00]; Precode of System.String.Ctor(System.ReadOnlySpan`1<Char>)
       mov       rdi,rax
       mov       rbp,[rbx+8]
       xor       eax,eax
       mov       [rbx+8],rax
       mov       [rsi],rax
       mov       [rsi+8],rax
       mov       [rbx+10],eax
       test      rbp,rbp
       je        short M03_L00
       call      qword ptr [7FF8F43A9080]
       mov       rcx,[rax]
       mov       rdx,rbp
       xor       r8d,r8d
       cmp       [rcx],ecx
       call      qword ptr [7FF8F43C8880]; Precode of System.Buffers.SharedArrayPool`1[[System.Char, System.Private.CoreLib]].Return(Char[], Boolean)
M03_L00:
       mov       rax,rdi
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M03_L01:
       call      qword ptr [7FF8F43BE290]
       int       3
; Total bytes of code 126
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-10]
       mov       rdx,rax
       test      dl,1
       jne       short M04_L00
       ret
M04_L00:
       jmp       qword ptr [7FF8B92EE7A8]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-18]
       mov       rdx,rax
       test      dl,1
       jne       short M05_L00
       ret
M05_L00:
       jmp       qword ptr [7FF8B9105C38]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.__Canon, System.Private.CoreLib]](System.__Canon)
       push      rsi
       push      rbx
       sub       rsp,58
       xor       eax,eax
       mov       [rsp+28],rax
       xorps     xmm4,xmm4
       movaps    [rsp+30],xmm4
       mov       [rsp+40],rax
       mov       [rsp+50],rdx
       mov       rbx,rcx
       mov       rcx,rdx
       mov       rsi,r8
       cmp       byte ptr [rbx+14],0
       jne       near ptr M06_L05
       test      rsi,rsi
       je        near ptr M06_L06
       mov       rcx,rsi
       call      qword ptr [7FF8F43B6148]
       test      rax,rax
       jne       short M06_L01
       mov       rcx,rsi
       lea       r11,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       call      qword ptr [r11]
       mov       rdx,rax
M06_L00:
       test      rdx,rdx
       je        near ptr M06_L06
       lea       r8,[rbx+18]
       mov       ecx,[rbx+10]
       mov       eax,[r8+8]
       cmp       ecx,eax
       ja        near ptr M06_L07
       mov       r8,[r8]
       mov       r10d,ecx
       lea       r10,[r8+r10*2]
       sub       eax,ecx
       mov       esi,[rdx+8]
       cmp       esi,eax
       ja        near ptr M06_L08
       mov       r8d,esi
       add       r8,r8
       add       rdx,0C
       mov       rcx,r10
       call      qword ptr [7FF8F43BC900]; Precode of System.SpanHelpers.Memmove(Byte ByRef, Byte ByRef, UIntPtr)
       add       [rbx+10],esi
       jmp       near ptr M06_L06
M06_L01:
       mov       rcx,rsi
       call      qword ptr [7FF8F43B6180]
       test      rax,rax
       je        near ptr M06_L04
       mov       rcx,rsi
       call      qword ptr [7FF8F43B73C0]
       mov       rsi,rax
M06_L02:
       mov       rcx,rsi
       lea       rdx,[rbx+18]
       mov       r9d,[rbx+10]
       mov       r8d,[rdx+8]
       cmp       r9d,r8d
       ja        near ptr M06_L07
       mov       rdx,[rdx]
       mov       r11d,r9d
       lea       rdx,[rdx+r11*2]
       sub       r8d,r9d
       mov       [rsp+38],rdx
       mov       [rsp+40],r8d
       xor       edx,edx
       mov       [rsp+28],rdx
       mov       [rsp+30],edx
       mov       rdx,[rbx]
       mov       [rsp+20],rdx
       lea       rdx,[rsp+38]
       lea       r9,[rsp+28]
       lea       r8,[rsp+48]
       lea       r11,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       call      qword ptr [r11]
       test      eax,eax
       jne       short M06_L03
       mov       rcx,rbx
       call      qword ptr [7FF8F43C3FC0]
       jmp       short M06_L02
M06_L03:
       mov       ecx,[rsp+48]
       add       [rbx+10],ecx
       jmp       short M06_L06
M06_L04:
       mov       rcx,rsi
       call      qword ptr [7FF8F43B73B8]
       mov       rcx,rax
       mov       r8,[rbx]
       lea       r11,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       xor       edx,edx
       call      qword ptr [r11]
       mov       rdx,rax
       jmp       near ptr M06_L00
M06_L05:
       call      qword ptr [7FF8F43AF0C8]
       mov       rdx,rax
       mov       rcx,rbx
       mov       r8,rsi
       xor       r9d,r9d
       call      qword ptr [7FF8F43D3628]
M06_L06:
       nop
       add       rsp,58
       pop       rbx
       pop       rsi
       ret
M06_L07:
       call      qword ptr [7FF8F43BE290]
       int       3
M06_L08:
       mov       rcx,rbx
       call      qword ptr [7FF8F43C3F98]
       jmp       short M06_L06
; Total bytes of code 397
```

## .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3 (Job: .NET 10.0(Runtime=.NET 10.0, Toolchain=net10.0))

```assembly
; Nalix.Benchmark.Framework.Serialization.StructBenchmarks.Serialize()
       push      rbx
       sub       rsp,20
       mov       rbx,[rcx+10]
       add       rcx,18
       call      qword ptr [7FF8B947FBB8]; Nalix.Framework.Serialization.LiteSerializer.Serialize[[Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct, Nalix.Benchmark.Framework]](ComplexStruct ByRef)
       lea       rcx,[rbx+8]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
       xor       eax,eax
       mov       [rbx+8],rax
       add       rsp,20
       pop       rbx
       ret
; Total bytes of code 43
```
```assembly
; Nalix.Framework.Serialization.LiteSerializer.Serialize[[Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct, Nalix.Benchmark.Framework]](ComplexStruct ByRef)
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,98
       lea       rbp,[rsp+0B0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqa   xmmword ptr [rbp-30],xmm4
       xor       eax,eax
       mov       [rbp-20],rax
       mov       rsi,rcx
       test      byte ptr [7FF8B94D8970],1
       je        near ptr M01_L28
M01_L00:
       test      byte ptr [7FF8B94D9958],1
       je        near ptr M01_L29
M01_L01:
       cmp       byte ptr [7FF8B904B0FA],0
       jne       short M01_L02
       movzx     ecx,byte ptr [7FF8B904B0F9]
       test      ecx,ecx
       je        short M01_L04
M01_L02:
       cmp       byte ptr [7FF8B904B0FB],0
       jne       near ptr M01_L06
       cmp       byte ptr [7FF8B904B0FC],0
       je        near ptr M01_L05
       mov       eax,2
M01_L03:
       cmp       eax,1
       jne       near ptr M01_L07
       cmp       [rsi],sil
       mov       rcx,offset MT_Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct
       call      CORINFO_HELP_NEWSFAST
       mov       rdx,rax
       lea       rdi,[rdx+8]
       call      CORINFO_HELP_ASSIGN_BYREF
       call      CORINFO_HELP_ASSIGN_BYREF
       mov       ecx,5
       rep movsq
       mov       rcx,offset MT_System.Array
       call      qword ptr [7FF8B9106328]; System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       int       3
M01_L04:
       mov       rcx,offset MT_System.Byte[]
       mov       edx,38
       call      CORINFO_HELP_NEWARR_1_VC
       mov       rdx,rax
       lea       rdi,[rdx+10]
       call      CORINFO_HELP_ASSIGN_BYREF
       call      CORINFO_HELP_ASSIGN_BYREF
       mov       ecx,5
       rep movsq
       mov       rax,rdx
       vzeroupper
       add       rsp,98
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L05:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L06:
       mov       eax,1
       jmp       near ptr M01_L03
M01_L07:
       cmp       eax,2
       jne       near ptr M01_L21
       test      byte ptr [7FF8B94DA728],1
       je        near ptr M01_L30
M01_L08:
       cmp       byte ptr [7FF8B904B100],0
       je        short M01_L09
       cmp       [rsi],sil
M01_L09:
       mov       ecx,80001328
       mov       rbx,[rcx]
       mov       byte ptr [rbp-2C],1
       test      byte ptr [7FF8B94DA928],1
       je        near ptr M01_L31
M01_L10:
       mov       edx,80001330
       mov       rax,[rdx]
       mov       edx,800
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
       mov       [rbp-38],rax
       mov       r8,[rbp-38]
       test      r8,r8
       je        near ptr M01_L32
       lea       rdx,[r8+10]
       mov       r8d,[r8+8]
M01_L11:
       mov       [rbp-28],rdx
       mov       [rbp-20],r8d
       xor       r8d,r8d
       mov       [rbp-30],r8d
       vmovdqu   ymm0,ymmword ptr [rsi]
       vmovdqu   ymmword ptr [rbp-90],ymm0
       vmovdqu   ymm0,ymmword ptr [rsi+18]
       vmovdqu   ymmword ptr [rbp-78],ymm0
       lea       r8,[rbp-90]
       lea       rdx,[rbp-38]
       mov       rcx,rbx
       mov       r11,7FF8B9050410
       call      qword ptr [r11]
       cmp       dword ptr [rbp-30],0
       je        short M01_L13
       mov       esi,[rbp-30]
       movsxd    rdx,esi
       mov       rcx,offset MT_System.Byte[]
       call      CORINFO_HELP_NEWARR_1_VC
       mov       rbx,rax
       test      esi,esi
       jle       short M01_L12
       mov       rdx,[rbp-28]
       mov       r8d,[rbp-20]
       cmp       esi,r8d
       ja        short M01_L15
       lea       rcx,[rbx+10]
       mov       r8d,[rbx+8]
       mov       r8d,esi
       call      qword ptr [7FF8B9105818]; System.SpanHelpers.Memmove(Byte ByRef, Byte ByRef, UIntPtr)
M01_L12:
       jmp       short M01_L17
M01_L13:
       test      byte ptr [7FF8B94DA298],1
       je        short M01_L16
M01_L14:
       mov       ecx,80001320
       mov       rbx,[rcx]
       jmp       short M01_L12
M01_L15:
       call      qword ptr [7FF8B92E7D50]
       int       3
M01_L16:
       mov       rcx,offset MT_System.Array+EmptyArray<System.Byte>
       call      qword ptr [7FF8B9105728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       short M01_L14
M01_L17:
       cmp       qword ptr [rbp-38],0
       je        short M01_L19
       cmp       byte ptr [rbp-2C],0
       je        short M01_L18
       mov       rdx,[rbp-38]
       mov       rsi,rdx
       mov       edx,80001338
       mov       rax,[rdx]
       mov       rdx,rsi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L18:
       xor       ecx,ecx
       mov       [rbp-38],rcx
M01_L19:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-28],xmm0
       xor       ecx,ecx
       mov       [rbp-30],ecx
M01_L20:
       mov       rax,rbx
       vzeroupper
       add       rsp,98
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L21:
       test      eax,eax
       jne       near ptr M01_L36
       test      byte ptr [7FF8B94DA728],1
       je        near ptr M01_L33
M01_L22:
       cmp       byte ptr [7FF8B904B100],0
       je        short M01_L23
       cmp       [rsi],sil
M01_L23:
       mov       ecx,80001328
       mov       rbx,[rcx]
       mov       byte ptr [rbp-4C],1
       test      byte ptr [7FF8B94DA928],1
       je        near ptr M01_L34
M01_L24:
       mov       edx,80001330
       mov       rax,[rdx]
       mov       edx,800
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
       mov       [rbp-58],rax
       mov       r8,[rbp-58]
       test      r8,r8
       je        near ptr M01_L35
       lea       rdx,[r8+10]
       mov       r8d,[r8+8]
M01_L25:
       mov       [rbp-48],rdx
       mov       [rbp-40],r8d
       xor       r8d,r8d
       mov       [rbp-50],r8d
       vmovdqu   ymm0,ymmword ptr [rsi]
       vmovdqu   ymmword ptr [rbp-90],ymm0
       vmovdqu   ymm0,ymmword ptr [rsi+18]
       vmovdqu   ymmword ptr [rbp-78],ymm0
       lea       r8,[rbp-90]
       lea       rdx,[rbp-58]
       mov       rcx,rbx
       mov       r11,7FF8B9050408
       call      qword ptr [r11]
       lea       rcx,[rbp-58]
       call      qword ptr [7FF8B947FF48]
       mov       rbx,rax
       cmp       qword ptr [rbp-58],0
       je        short M01_L27
       cmp       byte ptr [rbp-4C],0
       je        short M01_L26
       mov       rdx,[rbp-58]
       mov       rsi,rdx
       mov       edx,80001338
       mov       rax,[rdx]
       mov       rdx,rsi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L26:
       xor       eax,eax
       mov       [rbp-58],rax
M01_L27:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-48],xmm0
       xor       eax,eax
       mov       [rbp-50],eax
       jmp       near ptr M01_L20
M01_L28:
       mov       rcx,offset MT_Nalix.Framework.Serialization.Internal.Types.TypeMetadata
       call      qword ptr [7FF8B9105728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L00
M01_L29:
       mov       rcx,offset MT_Nalix.Framework.Serialization.Internal.Types.TypeMetadata+Cache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9105740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L01
M01_L30:
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9105740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L08
M01_L31:
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9105728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L10
M01_L32:
       xor       edx,edx
       xor       r8d,r8d
       jmp       near ptr M01_L11
M01_L33:
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9105740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L22
M01_L34:
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9105728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L24
M01_L35:
       xor       edx,edx
       xor       r8d,r8d
       jmp       near ptr M01_L25
M01_L36:
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       ecx,2818
       mov       rdx,7FF8B94D7A68
       call      qword ptr [7FF8B910F210]
       mov       rsi,rax
       mov       ecx,150C8AE0
       call      qword ptr [7FF8B904A310]
       mov       rdi,rax
       mov       ecx,2824
       mov       rdx,7FF8B94D7A68
       call      qword ptr [7FF8B910F210]
       mov       r8,rax
       mov       rcx,rsi
       mov       rdx,rdi
       call      qword ptr [7FF8B92EECE8]; System.String.Concat(System.String, System.String, System.String)
       mov       rsi,rax
       mov       rcx,rbx
       call      qword ptr [7FF8B947FF60]
       lea       rcx,[rbx+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rbx
       call      CORINFO_HELP_THROW
       int       3
       sub       rsp,28
       mov       rbx,[rbp-38]
       test      rbx,rbx
       je        short M01_L39
       cmp       byte ptr [rbp-2C],0
       je        short M01_L38
       test      byte ptr [7FF8B94DA928],1
       jne       short M01_L37
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9105728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
M01_L37:
       mov       edx,80001338
       mov       rax,[rdx]
       mov       rdx,rbx
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L38:
       xor       edx,edx
       mov       [rbp-38],rdx
M01_L39:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-28],xmm0
       xor       edx,edx
       mov       [rbp-30],edx
       vzeroupper
       add       rsp,28
       ret
       sub       rsp,28
       mov       rbx,[rbp-58]
       test      rbx,rbx
       je        short M01_L42
       cmp       byte ptr [rbp-4C],0
       je        short M01_L41
       test      byte ptr [7FF8B94DA928],1
       jne       short M01_L40
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9105728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
M01_L40:
       mov       edx,80001338
       mov       rax,[rdx]
       mov       rdx,rbx
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L41:
       xor       edx,edx
       mov       [rbp-58],rdx
M01_L42:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-48],xmm0
       xor       edx,edx
       mov       [rbp-50],edx
       vzeroupper
       add       rsp,28
       ret
; Total bytes of code 1353
```

## .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3 (Job: Net10(MinIterationTime=250ms, Affinity=0000000000000001, PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c, Runtime=.NET 10.0, Server=True, IterationCount=10, LaunchCount=1, RunStrategy=Throughput, WarmupCount=6))

```assembly
; Nalix.Benchmark.Framework.Serialization.StructBenchmarks.Serialize_IntoSpan()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0F0
       lea       rbp,[rsp+110]
       vxorps    xmm4,xmm4,xmm4
       vmovdqa   xmmword ptr [rbp-0F0],xmm4
       mov       rax,0FFFFFFFFFFFFFF40
M00_L00:
       vmovdqa   xmmword ptr [rbp+rax-20],xmm4
       vmovdqa   xmmword ptr [rbp+rax-10],xmm4
       vmovdqa   xmmword ptr [rax+rbp],xmm4
       add       rax,30
       jne       short M00_L00
       mov       rbx,[rcx+10]
       lea       rsi,[rcx+18]
       mov       rdi,[rcx+8]
       test      rdi,rdi
       je        short M00_L02
       cmp       byte ptr [7FF8B907B0FA],0
       jne       short M00_L03
       movzx     ecx,byte ptr [7FF8B907B0F9]
       test      ecx,ecx
       jne       short M00_L03
       cmp       dword ptr [rdi+8],38
       jl        short M00_L04
       add       rdi,10
       call      CORINFO_HELP_ASSIGN_BYREF
       call      CORINFO_HELP_ASSIGN_BYREF
       mov       ecx,5
       rep movsq
       mov       esi,38
M00_L01:
       mov       [rbx+38],esi
       vzeroupper
       add       rsp,0F0
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       ecx,2850
       mov       rdx,7FF8B9530F78
       call      qword ptr [7FF8B913F210]
       mov       rcx,rax
       call      qword ptr [7FF8B9527360]
       int       3
M00_L03:
       cmp       byte ptr [7FF8B907B0FB],0
       je        near ptr M00_L10
       jmp       near ptr M00_L09
M00_L04:
       lea       rcx,[rbp-0B8]
       mov       edx,26
       mov       r8d,2
       call      qword ptr [7FF8B913C270]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       mov       ecx,[rbp-0A8]
       cmp       ecx,[rbp-98]
       ja        near ptr M00_L23
       mov       rdx,[rbp-0A0]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-98]
       sub       eax,ecx
       cmp       eax,1C
       jb        short M00_L05
       vmovups   ymm0,[7FF8B91B0060]
       vmovups   [rdx],ymm0
       vmovups   xmm0,[7FF8B91B0080]
       vmovups   [rdx+20],xmm0
       mov       rcx,20003A00640065
       mov       [rdx+30],rcx
       mov       ecx,[rbp-0A8]
       add       ecx,1C
       mov       [rbp-0A8],ecx
       jmp       short M00_L06
M00_L05:
       lea       rcx,[rbp-0B8]
       mov       edx,15089028
       call      qword ptr [7FF8B94AC5D0]
M00_L06:
       lea       rcx,[rbp-0B8]
       mov       edx,38
       call      qword ptr [7FF8B9137FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       ecx,[rbp-0A8]
       cmp       ecx,[rbp-98]
       ja        near ptr M00_L23
       mov       rdx,[rbp-0A0]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-98]
       sub       eax,ecx
       cmp       eax,0A
       jb        short M00_L07
       vmovups   xmm0,[7FF8B91B0090]
       vmovups   [rdx],xmm0
       mov       dword ptr [rdx+10],20003A
       mov       ecx,[rbp-0A8]
       add       ecx,0A
       mov       [rbp-0A8],ecx
       jmp       short M00_L08
M00_L07:
       lea       rcx,[rbp-0B8]
       mov       edx,15089078
       call      qword ptr [7FF8B94AC5D0]
M00_L08:
       mov       edx,[rdi+8]
       lea       rcx,[rbp-0B8]
       call      qword ptr [7FF8B9137FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       lea       rcx,[rbp-0B8]
       call      qword ptr [7FF8B913C2A0]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       mov       rsi,rax
       mov       rcx,rdi
       call      qword ptr [7FF8B9527390]
       lea       rcx,[rdi+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rdi
       call      CORINFO_HELP_THROW
       int       3
M00_L09:
       mov       r14d,[7FF8B907B0F4]
       mov       ecx,1
       jmp       short M00_L12
M00_L10:
       cmp       byte ptr [7FF8B907B0FC],0
       je        short M00_L11
       mov       r14d,[7FF8B907B0F0]
       mov       ecx,2
       jmp       short M00_L12
M00_L11:
       xor       r14d,r14d
       xor       ecx,ecx
M00_L12:
       cmp       ecx,2
       jne       near ptr M00_L20
       cmp       [rdi+8],r14d
       jge       near ptr M00_L17
       lea       rcx,[rbp-90]
       mov       edx,26
       mov       r8d,2
       call      qword ptr [7FF8B913C270]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       mov       ecx,[rbp-80]
       cmp       ecx,[rbp-70]
       ja        near ptr M00_L23
       mov       rdx,[rbp-78]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-70]
       sub       eax,ecx
       cmp       eax,1C
       jb        short M00_L13
       vmovups   ymm0,[7FF8B91B0060]
       vmovups   [rdx],ymm0
       vmovups   xmm0,[7FF8B91B0080]
       vmovups   [rdx+20],xmm0
       mov       rcx,20003A00640065
       mov       [rdx+30],rcx
       mov       ecx,[rbp-80]
       add       ecx,1C
       mov       [rbp-80],ecx
       jmp       short M00_L14
M00_L13:
       lea       rcx,[rbp-90]
       mov       edx,15089028
       call      qword ptr [7FF8B94AC5D0]
M00_L14:
       lea       rcx,[rbp-90]
       mov       edx,r14d
       call      qword ptr [7FF8B9137FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       ecx,[rbp-80]
       cmp       ecx,[rbp-70]
       ja        near ptr M00_L23
       mov       rdx,[rbp-78]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-70]
       sub       eax,ecx
       cmp       eax,0A
       jb        short M00_L15
       vmovups   xmm0,[7FF8B91B0090]
       vmovups   [rdx],xmm0
       mov       dword ptr [rdx+10],20003A
       mov       ecx,[rbp-80]
       add       ecx,0A
       mov       [rbp-80],ecx
       jmp       short M00_L16
M00_L15:
       lea       rcx,[rbp-90]
       mov       edx,15089078
       call      qword ptr [7FF8B94AC5D0]
M00_L16:
       mov       edx,[rdi+8]
       lea       rcx,[rbp-90]
       call      qword ptr [7FF8B9137FD8]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF8B913C2A0]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       mov       rsi,rax
       mov       rcx,rbx
       call      qword ptr [7FF8B9527390]
       lea       rcx,[rbx+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rbx
       call      CORINFO_HELP_THROW
       int       3
M00_L17:
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9135740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9135740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       ecx,800013F8
       mov       r14,[rcx]
       lea       rcx,[rbp-68]
       mov       rdx,rdi
       call      qword ptr [7FF8B9524468]
       nop
       vmovdqu   ymm0,ymmword ptr [rsi]
       vmovdqu   ymmword ptr [rbp-0F0],ymm0
       vmovdqu   ymm0,ymmword ptr [rsi+18]
       vmovdqu   ymmword ptr [rbp-0D8],ymm0
       lea       r8,[rbp-0F0]
       lea       rdx,[rbp-68]
       mov       rcx,r14
       mov       r11,7FF8B9080510
       call      qword ptr [r11]
       mov       esi,[rbp-60]
       cmp       qword ptr [rbp-68],0
       je        short M00_L19
       cmp       byte ptr [rbp-5C],0
       je        short M00_L18
       mov       rcx,[rbp-68]
       mov       rdi,rcx
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9135728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       edx,80001408
       mov       rax,[rdx]
       mov       rdx,rdi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M00_L18:
       xor       ecx,ecx
       mov       [rbp-68],rcx
M00_L19:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-58],xmm0
       xor       ecx,ecx
       mov       [rbp-60],ecx
       jmp       near ptr M00_L01
M00_L20:
       lea       rcx,[rbp-48]
       mov       edx,63
       mov       r8d,1
       call      qword ptr [7FF8B913C270]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       mov       ecx,[rbp-38]
       cmp       ecx,[rbp-28]
       ja        near ptr M00_L23
       mov       rdx,[rbp-30]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-28]
       sub       eax,ecx
       cmp       eax,34
       jb        short M00_L21
       vmovups   ymm0,[7FF8B91B00A0]
       vmovups   [rdx],ymm0
       vmovups   ymm0,[7FF8B91B00C0]
       vmovups   [rdx+20],ymm0
       vmovups   ymm0,[7FF8B91B00E0]
       vmovups   [rdx+40],ymm0
       mov       rcx,20006500700079
       mov       [rdx+60],rcx
       mov       ecx,[rbp-38]
       add       ecx,34
       mov       [rbp-38],ecx
       jmp       short M00_L22
M00_L21:
       lea       rcx,[rbp-48]
       mov       edx,15088FA8
       call      qword ptr [7FF8B94AC5D0]
M00_L22:
       lea       rcx,[rbp-48]
       mov       rdx,7FF8B9531F78
       mov       r8d,15088BF0
       call      qword ptr [7FF8B931EC40]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.__Canon, System.Private.CoreLib]](System.__Canon)
       mov       ecx,[rbp-38]
       cmp       ecx,[rbp-28]
       jbe       short M00_L24
M00_L23:
       call      qword ptr [7FF8B9317D50]
       int       3
M00_L24:
       mov       rdx,[rbp-30]
       mov       eax,ecx
       lea       rdx,[rdx+rax*2]
       mov       eax,[rbp-28]
       sub       eax,ecx
       cmp       eax,2F
       jb        short M00_L25
       vmovups   ymm0,[7FF8B91B0100]
       vmovups   [rdx],ymm0
       vmovups   ymm0,[7FF8B91B0120]
       vmovups   [rdx+20],ymm0
       vmovups   xmm0,[7FF8B91B0140]
       vmovups   [rdx+40],xmm0
       mov       rcx,6500740073006E
       mov       [rdx+50],rcx
       mov       dword ptr [rdx+58],640061
       mov       word ptr [rdx+5C],2E
       mov       ecx,[rbp-38]
       add       ecx,2F
       mov       [rbp-38],ecx
       jmp       short M00_L26
M00_L25:
       lea       rcx,[rbp-48]
       mov       edx,150890A8
       call      qword ptr [7FF8B94AC5D0]
M00_L26:
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rcx,[rbp-48]
       call      qword ptr [7FF8B913C2A0]; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       mov       rsi,rax
       mov       rcx,rbx
       call      qword ptr [7FF8B9527390]
       lea       rcx,[rbx+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rbx
       call      CORINFO_HELP_THROW
       int       3
       sub       rsp,28
       mov       rsi,[rbp-68]
       test      rsi,rsi
       je        short M00_L29
       cmp       byte ptr [rbp-5C],0
       je        short M00_L28
       test      byte ptr [7FF8B953FEB0],1
       jne       short M00_L27
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9135728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
M00_L27:
       mov       edx,80001408
       mov       rax,[rdx]
       mov       rdx,rsi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M00_L28:
       xor       ecx,ecx
       mov       [rbp-68],rcx
M00_L29:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-58],xmm0
       xor       ecx,ecx
       mov       [rbp-60],ecx
       vzeroupper
       add       rsp,28
       ret
; Total bytes of code 1538
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Int32, Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       rbx,rcx
       mov       esi,edx
       mov       edi,r8d
       xor       eax,eax
       mov       [rbx],rax
       call      qword ptr [7FF8F43A9080]
       mov       rcx,[rax]
       imul      edx,edi,0B
       add       edx,esi
       mov       eax,100
       cmp       edx,100
       cmovle    edx,eax
       cmp       [rcx],ecx
       call      qword ptr [7FF8F43C8878]; Precode of System.Buffers.SharedArrayPool`1[[System.Char, System.Private.CoreLib]].Rent(Int32)
       mov       [rbx+8],rax
       test      rax,rax
       je        short M01_L01
       lea       rcx,[rax+10]
       mov       eax,[rax+8]
M01_L00:
       mov       [rbx+18],rcx
       mov       [rbx+20],eax
       xor       eax,eax
       mov       [rbx+10],eax
       mov       byte ptr [rbx+14],0
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M01_L01:
       xor       ecx,ecx
       xor       eax,eax
       jmp       short M01_L00
; Total bytes of code 102
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.Int32, System.Private.CoreLib]](Int32)
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,58
       xorps     xmm4,xmm4
       movaps    [rsp+30],xmm4
       movaps    [rsp+40],xmm4
       mov       rbx,rcx
       mov       esi,edx
       cmp       byte ptr [rbx+14],0
       jne       short M02_L03
M02_L00:
       lea       rdx,[rbx+18]
       mov       r8d,[rbx+10]
       mov       edi,[rdx+8]
       cmp       r8d,edi
       ja        near ptr M02_L10
       mov       rdx,[rdx]
       mov       ecx,r8d
       lea       rbp,[rdx+rcx*2]
       sub       edi,r8d
       mov       rcx,[rbx]
       test      esi,esi
       jl        short M02_L05
       mov       [rsp+40],rbp
       mov       [rsp+48],edi
       lea       rdx,[rsp+40]
       lea       r8,[rsp+50]
       mov       ecx,esi
       call      qword ptr [7FF8F43CFCA8]; Precode of System.Number.TryUInt32ToDecStr[[System.Char, System.Private.CoreLib]](UInt32, System.Span`1<Char>, Int32 ByRef)
M02_L01:
       test      eax,eax
       jne       short M02_L02
       mov       rcx,rbx
       call      qword ptr [7FF8F43C3FC0]
       jmp       short M02_L00
M02_L02:
       mov       eax,[rsp+50]
       add       [rbx+10],eax
       jmp       short M02_L04
M02_L03:
       mov       rcx,rbx
       mov       edx,esi
       xor       r8d,r8d
       call      qword ptr [7FF8F43D0B00]
M02_L04:
       nop
       add       rsp,58
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M02_L05:
       test      rcx,rcx
       je        short M02_L06
       call      qword ptr [7FF8F43C0238]; Precode of System.Globalization.NumberFormatInfo.<GetInstance>g__GetProviderNonNull|58_0(System.IFormatProvider)
       jmp       short M02_L07
M02_L06:
       call      qword ptr [7FF8F43C0220]; Precode of System.Globalization.NumberFormatInfo.get_CurrentInfo()
M02_L07:
       mov       r8,[rax+28]
       test      r8,r8
       jne       short M02_L08
       xor       r9d,r9d
       xor       r8d,r8d
       jmp       short M02_L09
M02_L08:
       lea       r9,[r8+0C]
       mov       r8d,[r8+8]
M02_L09:
       mov       [rsp+30],r9
       mov       [rsp+38],r8d
       mov       [rsp+40],rbp
       mov       [rsp+48],edi
       lea       r8,[rsp+50]
       mov       [rsp+20],r8
       lea       r8,[rsp+30]
       lea       r9,[rsp+40]
       mov       ecx,esi
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF8F43CFC90]
       jmp       near ptr M02_L01
M02_L10:
       call      qword ptr [7FF8F43BE290]
       int       3
; Total bytes of code 255
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear()
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rbx,rcx
       lea       rsi,[rbx+18]
       mov       rcx,rsi
       mov       eax,[rbx+10]
       cmp       eax,[rcx+8]
       ja        short M03_L01
       mov       rcx,[rcx]
       mov       [rsp+28],rcx
       mov       [rsp+30],eax
       lea       rcx,[rsp+28]
       call      qword ptr [7FF8F43BAB00]; Precode of System.String.Ctor(System.ReadOnlySpan`1<Char>)
       mov       rdi,rax
       mov       rbp,[rbx+8]
       xor       eax,eax
       mov       [rbx+8],rax
       mov       [rsi],rax
       mov       [rsi+8],rax
       mov       [rbx+10],eax
       test      rbp,rbp
       je        short M03_L00
       call      qword ptr [7FF8F43A9080]
       mov       rcx,[rax]
       mov       rdx,rbp
       xor       r8d,r8d
       cmp       [rcx],ecx
       call      qword ptr [7FF8F43C8880]; Precode of System.Buffers.SharedArrayPool`1[[System.Char, System.Private.CoreLib]].Return(Char[], Boolean)
M03_L00:
       mov       rax,rdi
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M03_L01:
       call      qword ptr [7FF8F43BE290]
       int       3
; Total bytes of code 126
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       push      rsi
       push      rbx
       sub       rsp,28
       mov       rbx,rcx
       mov       rcx,[rbx+20]
       mov       rsi,[rcx-10]
       mov       rcx,rsi
       test      cl,1
       jne       short M04_L00
       mov       rcx,7FF8B953F674
       call      CORINFO_HELP_COUNTPROFILE32
       mov       rax,rsi
       add       rsp,28
       pop       rbx
       pop       rsi
       ret
M04_L00:
       mov       rcx,7FF8B953F670
       call      CORINFO_HELP_COUNTPROFILE32
       mov       rcx,rbx
       add       rsp,28
       pop       rbx
       pop       rsi
       jmp       qword ptr [7FF8B931E9D0]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 80
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-18]
       mov       rdx,rax
       test      dl,1
       jne       short M05_L00
       ret
M05_L00:
       jmp       qword ptr [7FF8B9135C38]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted[[System.__Canon, System.Private.CoreLib]](System.__Canon)
       push      rsi
       push      rbx
       sub       rsp,58
       xor       eax,eax
       mov       [rsp+28],rax
       xorps     xmm4,xmm4
       movaps    [rsp+30],xmm4
       mov       [rsp+40],rax
       mov       [rsp+50],rdx
       mov       rbx,rcx
       mov       rcx,rdx
       mov       rsi,r8
       cmp       byte ptr [rbx+14],0
       jne       near ptr M06_L05
       test      rsi,rsi
       je        near ptr M06_L06
       mov       rcx,rsi
       call      qword ptr [7FF8F43B6148]
       test      rax,rax
       jne       short M06_L01
       mov       rcx,rsi
       lea       r11,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       call      qword ptr [r11]
       mov       rdx,rax
M06_L00:
       test      rdx,rdx
       je        near ptr M06_L06
       lea       r8,[rbx+18]
       mov       ecx,[rbx+10]
       mov       eax,[r8+8]
       cmp       ecx,eax
       ja        near ptr M06_L07
       mov       r8,[r8]
       mov       r10d,ecx
       lea       r10,[r8+r10*2]
       sub       eax,ecx
       mov       esi,[rdx+8]
       cmp       esi,eax
       ja        near ptr M06_L08
       mov       r8d,esi
       add       r8,r8
       add       rdx,0C
       mov       rcx,r10
       call      qword ptr [7FF8F43BC900]; Precode of System.SpanHelpers.Memmove(Byte ByRef, Byte ByRef, UIntPtr)
       add       [rbx+10],esi
       jmp       near ptr M06_L06
M06_L01:
       mov       rcx,rsi
       call      qword ptr [7FF8F43B6180]
       test      rax,rax
       je        near ptr M06_L04
       mov       rcx,rsi
       call      qword ptr [7FF8F43B73C0]
       mov       rsi,rax
M06_L02:
       mov       rcx,rsi
       lea       rdx,[rbx+18]
       mov       r9d,[rbx+10]
       mov       r8d,[rdx+8]
       cmp       r9d,r8d
       ja        near ptr M06_L07
       mov       rdx,[rdx]
       mov       r11d,r9d
       lea       rdx,[rdx+r11*2]
       sub       r8d,r9d
       mov       [rsp+38],rdx
       mov       [rsp+40],r8d
       xor       edx,edx
       mov       [rsp+28],rdx
       mov       [rsp+30],edx
       mov       rdx,[rbx]
       mov       [rsp+20],rdx
       lea       rdx,[rsp+38]
       lea       r9,[rsp+28]
       lea       r8,[rsp+48]
       lea       r11,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       call      qword ptr [r11]
       test      eax,eax
       jne       short M06_L03
       mov       rcx,rbx
       call      qword ptr [7FF8F43C3FC0]
       jmp       short M06_L02
M06_L03:
       mov       ecx,[rsp+48]
       add       [rbx+10],ecx
       jmp       short M06_L06
M06_L04:
       mov       rcx,rsi
       call      qword ptr [7FF8F43B73B8]
       mov       rcx,rax
       mov       r8,[rbx]
       lea       r11,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       xor       edx,edx
       call      qword ptr [r11]
       mov       rdx,rax
       jmp       near ptr M06_L00
M06_L05:
       call      qword ptr [7FF8F43AF0C8]
       mov       rdx,rax
       mov       rcx,rbx
       mov       r8,rsi
       xor       r9d,r9d
       call      qword ptr [7FF8F43D3628]
M06_L06:
       nop
       add       rsp,58
       pop       rbx
       pop       rsi
       ret
M06_L07:
       call      qword ptr [7FF8F43BE290]
       int       3
M06_L08:
       mov       rcx,rbx
       call      qword ptr [7FF8F43C3F98]
       jmp       short M06_L06
; Total bytes of code 397
```

## .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3 (Job: Net10(MinIterationTime=250ms, Affinity=0000000000000001, PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c, Runtime=.NET 10.0, Server=True, IterationCount=10, LaunchCount=1, RunStrategy=Throughput, WarmupCount=6))

```assembly
; Nalix.Benchmark.Framework.Serialization.StructBenchmarks.Serialize()
       push      rbx
       sub       rsp,20
       mov       rbx,[rcx+10]
       add       rcx,18
       call      qword ptr [7FF8B95042E8]; Nalix.Framework.Serialization.LiteSerializer.Serialize[[Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct, Nalix.Benchmark.Framework]](ComplexStruct ByRef)
       lea       rcx,[rbx+8]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
       xor       eax,eax
       mov       [rbx+8],rax
       add       rsp,20
       pop       rbx
       ret
; Total bytes of code 43
```
```assembly
; Nalix.Framework.Serialization.LiteSerializer.Serialize[[Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct, Nalix.Benchmark.Framework]](ComplexStruct ByRef)
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,98
       lea       rbp,[rsp+0B0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       vmovdqa   xmmword ptr [rbp-30],xmm4
       xor       eax,eax
       mov       [rbp-20],rax
       mov       rsi,rcx
       test      byte ptr [7FF8B9511E80],1
       je        near ptr M01_L28
M01_L00:
       test      byte ptr [7FF8B9512E68],1
       je        near ptr M01_L29
M01_L01:
       cmp       byte ptr [7FF8B905B0FA],0
       jne       short M01_L02
       movzx     ecx,byte ptr [7FF8B905B0F9]
       test      ecx,ecx
       je        short M01_L04
M01_L02:
       cmp       byte ptr [7FF8B905B0FB],0
       jne       near ptr M01_L06
       cmp       byte ptr [7FF8B905B0FC],0
       je        near ptr M01_L05
       mov       eax,2
M01_L03:
       cmp       eax,1
       jne       near ptr M01_L07
       cmp       [rsi],sil
       mov       rcx,offset MT_Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct
       call      CORINFO_HELP_NEWSFAST
       mov       rdx,rax
       lea       rdi,[rdx+8]
       call      CORINFO_HELP_ASSIGN_BYREF
       call      CORINFO_HELP_ASSIGN_BYREF
       mov       ecx,5
       rep movsq
       mov       rcx,offset MT_System.Array
       call      qword ptr [7FF8B9116328]; System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       int       3
M01_L04:
       mov       rcx,offset MT_System.Byte[]
       mov       edx,38
       call      CORINFO_HELP_NEWARR_1_VC
       mov       rdx,rax
       lea       rdi,[rdx+10]
       call      CORINFO_HELP_ASSIGN_BYREF
       call      CORINFO_HELP_ASSIGN_BYREF
       mov       ecx,5
       rep movsq
       mov       rax,rdx
       vzeroupper
       add       rsp,98
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L05:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L06:
       mov       eax,1
       jmp       near ptr M01_L03
M01_L07:
       cmp       eax,2
       jne       near ptr M01_L21
       test      byte ptr [7FF8B9513C38],1
       je        near ptr M01_L30
M01_L08:
       cmp       byte ptr [7FF8B905B100],0
       je        short M01_L09
       cmp       [rsi],sil
M01_L09:
       mov       ecx,80001350
       mov       rbx,[rcx]
       mov       byte ptr [rbp-2C],1
       test      byte ptr [7FF8B9513E38],1
       je        near ptr M01_L31
M01_L10:
       mov       edx,80001358
       mov       rax,[rdx]
       mov       edx,800
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
       mov       [rbp-38],rax
       mov       r8,[rbp-38]
       test      r8,r8
       je        near ptr M01_L32
       lea       rdx,[r8+10]
       mov       r8d,[r8+8]
M01_L11:
       mov       [rbp-28],rdx
       mov       [rbp-20],r8d
       xor       r8d,r8d
       mov       [rbp-30],r8d
       vmovdqu   ymm0,ymmword ptr [rsi]
       vmovdqu   ymmword ptr [rbp-90],ymm0
       vmovdqu   ymm0,ymmword ptr [rsi+18]
       vmovdqu   ymmword ptr [rbp-78],ymm0
       lea       r8,[rbp-90]
       lea       rdx,[rbp-38]
       mov       rcx,rbx
       mov       r11,7FF8B9060410
       call      qword ptr [r11]
       cmp       dword ptr [rbp-30],0
       je        short M01_L13
       mov       esi,[rbp-30]
       movsxd    rdx,esi
       mov       rcx,offset MT_System.Byte[]
       call      CORINFO_HELP_NEWARR_1_VC
       mov       rbx,rax
       test      esi,esi
       jle       short M01_L12
       mov       rdx,[rbp-28]
       mov       r8d,[rbp-20]
       cmp       esi,r8d
       ja        short M01_L15
       lea       rcx,[rbx+10]
       mov       r8d,[rbx+8]
       mov       r8d,esi
       call      qword ptr [7FF8B9115818]; System.SpanHelpers.Memmove(Byte ByRef, Byte ByRef, UIntPtr)
M01_L12:
       jmp       short M01_L17
M01_L13:
       test      byte ptr [7FF8B95137A8],1
       je        short M01_L16
M01_L14:
       mov       ecx,80001348
       mov       rbx,[rcx]
       jmp       short M01_L12
M01_L15:
       call      qword ptr [7FF8B92F7D50]
       int       3
M01_L16:
       mov       rcx,offset MT_System.Array+EmptyArray<System.Byte>
       call      qword ptr [7FF8B9115728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       short M01_L14
M01_L17:
       cmp       qword ptr [rbp-38],0
       je        short M01_L19
       cmp       byte ptr [rbp-2C],0
       je        short M01_L18
       mov       rdx,[rbp-38]
       mov       rsi,rdx
       mov       edx,80001360
       mov       rax,[rdx]
       mov       rdx,rsi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L18:
       xor       ecx,ecx
       mov       [rbp-38],rcx
M01_L19:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-28],xmm0
       xor       ecx,ecx
       mov       [rbp-30],ecx
M01_L20:
       mov       rax,rbx
       vzeroupper
       add       rsp,98
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L21:
       test      eax,eax
       jne       near ptr M01_L36
       test      byte ptr [7FF8B9513C38],1
       je        near ptr M01_L33
M01_L22:
       cmp       byte ptr [7FF8B905B100],0
       je        short M01_L23
       cmp       [rsi],sil
M01_L23:
       mov       ecx,80001350
       mov       rbx,[rcx]
       mov       byte ptr [rbp-4C],1
       test      byte ptr [7FF8B9513E38],1
       je        near ptr M01_L34
M01_L24:
       mov       edx,80001358
       mov       rax,[rdx]
       mov       edx,800
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
       mov       [rbp-58],rax
       mov       r8,[rbp-58]
       test      r8,r8
       je        near ptr M01_L35
       lea       rdx,[r8+10]
       mov       r8d,[r8+8]
M01_L25:
       mov       [rbp-48],rdx
       mov       [rbp-40],r8d
       xor       r8d,r8d
       mov       [rbp-50],r8d
       vmovdqu   ymm0,ymmword ptr [rsi]
       vmovdqu   ymmword ptr [rbp-90],ymm0
       vmovdqu   ymm0,ymmword ptr [rsi+18]
       vmovdqu   ymmword ptr [rbp-78],ymm0
       lea       r8,[rbp-90]
       lea       rdx,[rbp-58]
       mov       rcx,rbx
       mov       r11,7FF8B9060408
       call      qword ptr [r11]
       lea       rcx,[rbp-58]
       call      qword ptr [7FF8B9504678]
       mov       rbx,rax
       cmp       qword ptr [rbp-58],0
       je        short M01_L27
       cmp       byte ptr [rbp-4C],0
       je        short M01_L26
       mov       rdx,[rbp-58]
       mov       rsi,rdx
       mov       edx,80001360
       mov       rax,[rdx]
       mov       rdx,rsi
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L26:
       xor       eax,eax
       mov       [rbp-58],rax
M01_L27:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-48],xmm0
       xor       eax,eax
       mov       [rbp-50],eax
       jmp       near ptr M01_L20
M01_L28:
       mov       rcx,offset MT_Nalix.Framework.Serialization.Internal.Types.TypeMetadata
       call      qword ptr [7FF8B9115728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L00
M01_L29:
       mov       rcx,offset MT_Nalix.Framework.Serialization.Internal.Types.TypeMetadata+Cache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9115740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L01
M01_L30:
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9115740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L08
M01_L31:
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9115728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L10
M01_L32:
       xor       edx,edx
       xor       r8d,r8d
       jmp       near ptr M01_L11
M01_L33:
       mov       rcx,offset MT_Nalix.Framework.Serialization.LiteSerializer+RootFormatterCache<Nalix.Benchmark.Framework.Serialization.StructBenchmarks+ComplexStruct>
       call      qword ptr [7FF8B9115740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L22
M01_L34:
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9115728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M01_L24
M01_L35:
       xor       edx,edx
       xor       r8d,r8d
       jmp       near ptr M01_L25
M01_L36:
       mov       rcx,offset MT_Nalix.Common.Exceptions.SerializationFailureException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       ecx,2818
       mov       rdx,7FF8B9510F78
       call      qword ptr [7FF8B911F210]
       mov       rsi,rax
       mov       ecx,14F88BC8
       call      qword ptr [7FF8B905A310]
       mov       rdi,rax
       mov       ecx,2824
       mov       rdx,7FF8B9510F78
       call      qword ptr [7FF8B911F210]
       mov       r8,rax
       mov       rcx,rsi
       mov       rdx,rdi
       call      qword ptr [7FF8B92FEF10]; System.String.Concat(System.String, System.String, System.String)
       mov       rsi,rax
       mov       rcx,rbx
       call      qword ptr [7FF8B9504690]
       lea       rcx,[rbx+10]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,rbx
       call      CORINFO_HELP_THROW
       int       3
       sub       rsp,28
       mov       rbx,[rbp-38]
       test      rbx,rbx
       je        short M01_L39
       cmp       byte ptr [rbp-2C],0
       je        short M01_L38
       test      byte ptr [7FF8B9513E38],1
       jne       short M01_L37
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9115728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
M01_L37:
       mov       edx,80001360
       mov       rax,[rdx]
       mov       rdx,rbx
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L38:
       xor       edx,edx
       mov       [rbp-38],rdx
M01_L39:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-28],xmm0
       xor       edx,edx
       mov       [rbp-30],edx
       vzeroupper
       add       rsp,28
       ret
       sub       rsp,28
       mov       rbx,[rbp-58]
       test      rbx,rbx
       je        short M01_L42
       cmp       byte ptr [rbp-4C],0
       je        short M01_L41
       test      byte ptr [7FF8B9513E38],1
       jne       short M01_L40
       mov       rcx,offset MT_Nalix.Framework.Memory.Buffers.BufferLease+ByteArrayPool
       call      qword ptr [7FF8B9115728]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
M01_L40:
       mov       edx,80001360
       mov       rax,[rdx]
       mov       rdx,rbx
       mov       r8d,1
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
M01_L41:
       xor       edx,edx
       mov       [rbp-58],rdx
M01_L42:
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rbp-48],xmm0
       xor       edx,edx
       mov       [rbp-50],edx
       vzeroupper
       add       rsp,28
       ret
; Total bytes of code 1353
```

