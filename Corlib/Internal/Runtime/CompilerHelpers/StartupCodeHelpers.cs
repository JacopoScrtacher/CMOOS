using Internal.Runtime.CompilerServices;
using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;

namespace Internal.Runtime.CompilerHelpers
{
    public unsafe class StartupCodeHelpers
    {
        [RuntimeExport("RhpLdelemaRef")]
        public static unsafe ref object LdelemaRef(Array array, int index, IntPtr elementType)
        {
            //Debug.Assert(array.EEType->IsArray, "first argument must be an array");

            EEType* elemType = (EEType*)elementType;
            EEType* arrayElemType = array.EEType->RelatedParameterType;

            if (!AreTypesEquivalent(elemType, arrayElemType))
            {
                // Throw the array type mismatch exception defined by the classlib, using the input array's EEType* 
                // to find the correct classlib.

               // throw array.EEType->GetClasslibException(ExceptionIDs.ArrayTypeMismatch);
            }

            ref object rawData = ref Unsafe.As<byte, object>(ref Unsafe.As<RawArrayData>(array).Data);
            return ref Unsafe.Add(ref rawData, index);
        }

        [RuntimeExport("RhTypeCast_AreTypesEquivalent")]
        public static unsafe bool AreTypesEquivalent(EEType* pType1, EEType* pType2)
        {
            if (pType1 == pType2)
                return true;

            if (pType1->IsCloned)
                pType1 = pType1->CanonicalEEType;

            if (pType2->IsCloned)
                pType2 = pType2->CanonicalEEType;

            if (pType1 == pType2)
                return true;

            if (pType1->IsParameterizedType && pType2->IsParameterizedType)
                return AreTypesEquivalent(pType1->RelatedParameterType, pType2->RelatedParameterType) && pType1->ParameterizedTypeShape == pType2->ParameterizedTypeShape;

            return false;
        }

        [RuntimeExport("RhUnbox2")]
        public static unsafe ref byte RhUnbox2(EEType* pUnboxToEEType, object obj)
        {
            if ((obj == null) || !UnboxAnyTypeCompare(obj.EEType, pUnboxToEEType))
            {
                ExceptionIDs exID = obj == null ? ExceptionIDs.NullReference : ExceptionIDs.InvalidCast;
                //throw pUnboxToEEType->GetClasslibException(exID);
            }
            return ref obj.GetRawData();
        }

        static unsafe bool UnboxAnyTypeCompare(EEType* pEEType, EEType* ptrUnboxToEEType)
        {
            if (TypeCast.AreTypesEquivalent(pEEType, ptrUnboxToEEType))
                return true;

            if (pEEType->ElementType == ptrUnboxToEEType->ElementType)
            {
                // Enum's and primitive types should pass the UnboxAny exception cases
                // if they have an exactly matching cor element type.
                switch (ptrUnboxToEEType->ElementType)
                {
                    case EETypeElementType.Byte:
                    case EETypeElementType.SByte:
                    case EETypeElementType.Int16:
                    case EETypeElementType.UInt16:
                    case EETypeElementType.Int32:
                    case EETypeElementType.UInt32:
                    case EETypeElementType.Int64:
                    case EETypeElementType.UInt64:
                    case EETypeElementType.IntPtr:
                    case EETypeElementType.UIntPtr:
                        return true;
                }
            }

            return false;
        }

        [RuntimeExport("__imp_GetCurrentThreadId")]
        public static int __imp_GetCurrentThreadId() => 0;

        [RuntimeExport("__CheckForDebuggerJustMyCode")]
        public static int __CheckForDebuggerJustMyCode() => 0;

        [RuntimeExport("__fail_fast")]
        static void FailFast() { while (true) ; }

        [RuntimeExport("RhpFallbackFailFast")]
        static void RhpFallbackFailFast() { while (true) ; }

        [RuntimeExport("RhpReversePInvoke2")]
        static void RhpReversePInvoke2(IntPtr frame) { }

        [RuntimeExport("RhpReversePInvokeReturn2")]
        static void RhpReversePInvokeReturn2(IntPtr frame) { }

        [RuntimeExport("RhpReversePInvoke")]
        static void RhpReversePInvoke(IntPtr frame) { }

        [RuntimeExport("RhpReversePInvokeReturn")]
        static void RhpReversePInvokeReturn(IntPtr frame) { }

        [RuntimeExport("RhpPInvoke")]
        static void RhpPinvoke(IntPtr frame) { }

        [RuntimeExport("RhpPInvokeReturn")]
        static void RhpPinvokeReturn(IntPtr frame) { }

        [RuntimeExport("memset")]
        static unsafe void MemSet(byte* ptr, int c, int count)
        {
            for (byte* p = ptr; p < ptr + count; p++)
                *p = (byte)c;
        }

        [RuntimeExport("memcpy")]
        static unsafe void MemCpy(byte* dest, byte* src, ulong count)
        {
            for (ulong i = 0; i < count; i++) dest[i] = src[i];
        }

        [RuntimeExport("RhpNewFast")]
        static unsafe object RhpNewFast(EEType* pEEType)
        {
            var size = pEEType->BaseSize;

            // Round to next power of 8
            if (size % 8 > 0)
                size = ((size / 8) + 1) * 8;

            var data = malloc(size);
            var obj = Unsafe.As<IntPtr, object>(ref data);
            MemSet((byte*)data,0, (int)size);
            *(IntPtr*)data = (IntPtr)pEEType;

            return obj;
        }

        [DllImport("*")]
        public static extern nint malloc(ulong size);

        [RuntimeExport("RhpNewArray")]
        internal static unsafe object RhpNewArray(EEType* pEEType, int length)
        {
            var size = pEEType->BaseSize + (ulong)length * pEEType->ComponentSize;

            // Round to next power of 8
            if (size % 8 > 0)
                size = ((size / 8) + 1) * 8;

            var data = malloc(size);
            var obj = Unsafe.As<IntPtr, object>(ref data);
            MemSet((byte*)data,0, (int)size);
            *(IntPtr*)data = (IntPtr)pEEType;

            var b = (byte*)data;
            b += sizeof(IntPtr);
            MemCpy(b, (byte*)(&length), sizeof(int));

            return obj;
        }

        [RuntimeExport("RhpAssignRef")]
        static unsafe void RhpAssignRef(void** address, void* obj)
        {
            *address = obj;
        }

        [RuntimeExport("RhpByRefAssignRef")]
        static unsafe void RhpByRefAssignRef(void** address, void* obj)
        {
            *address = obj;
        }

        [RuntimeExport("RhpCheckedAssignRef")]
        static unsafe void RhpCheckedAssignRef(void** address, void* obj)
        {
            *address = obj;
        }

        [RuntimeExport("RhpStelemRef")]
        static unsafe void RhpStelemRef(Array array, int index, object obj)
        {
            fixed (int* n = &array._numComponents)
            {
                var ptr = (byte*)n;
                ptr += sizeof(void*);   // Array length is padded to 8 bytes on 64-bit
                ptr += index * array.EEType->ComponentSize;  // Component size should always be 8, seeing as it's a pointer...
                var pp = (IntPtr*)ptr;
                *pp = Unsafe.As<object, IntPtr>(ref obj);
            }
        }

        [RuntimeExport("RhTypeCast_IsInstanceOfClass")]
        public static unsafe object RhTypeCast_IsInstanceOfClass(EEType* pTargetType, object obj)
        {
            if (obj == null)
                return null;

            if (pTargetType == obj.EEType)
                return obj;

            var bt = obj.EEType->RawBaseType;

            
            while (true)
            {
                if (bt == null)
                    return null;

                if (pTargetType == bt)
                    return obj;

                bt = bt->RawBaseType;
            }
        }

        public static void InitializeModules(IntPtr Modules, bool KernelInitialization = false) 
        {
            for (int i = 0; ; i++)
            {
                if (((IntPtr*)Modules)[i].Equals(IntPtr.Zero))
                    break;

                var header = (ReadyToRunHeader*)((IntPtr*)Modules)[i];
                var sections = (ModuleInfoRow*)(header + 1);

                if(header->Signature != ReadyToRunHeaderConstants.Signature) 
                {
                    FailFast();
                }

                for (int k = 0; k < header->NumberOfSections; k++)
                {
                    if (sections[k].SectionId == ReadyToRunSectionType.GCStaticRegion)
                        InitializeStatics(sections[k].Start, sections[k].End);

                    if (sections[k].SectionId == ReadyToRunSectionType.EagerCctor)
                        RunEagerClassConstructors(sections[k].Start, sections[k].End);
                }
            }

            if (KernelInitialization)
            {
                DateTime.s_daysToMonth365 = new int[]{
                0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };
                DateTime.s_daysToMonth366 = new int[]{
                0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 };

                Encoding.UTF8 = new UTF8Encoding();
                Encoding.ASCII = new ASCIIEncoding();

                Random.Shared = new Random();
            }
        }

        private static unsafe void RunEagerClassConstructors(IntPtr cctorTableStart, IntPtr cctorTableEnd)
        {
            for (IntPtr* tab = (IntPtr*)cctorTableStart; tab < (IntPtr*)cctorTableEnd; tab++)
            {
                ((delegate*<void>)(*tab))();
            }
        }

        static unsafe void InitializeStatics(IntPtr rgnStart, IntPtr rgnEnd)
        {
            for (IntPtr* block = (IntPtr*)rgnStart; block < (IntPtr*)rgnEnd; block++)
            {
                var pBlock = (IntPtr*)*block;
                var blockAddr = (long)(*pBlock);

                if ((blockAddr & GCStaticRegionConstants.Uninitialized) == GCStaticRegionConstants.Uninitialized)
                {
                    var obj = RhpNewFast((EEType*)(blockAddr & ~GCStaticRegionConstants.Mask));

                    if ((blockAddr & GCStaticRegionConstants.HasPreInitializedData) == GCStaticRegionConstants.HasPreInitializedData)
                    {
                        IntPtr pPreInitDataAddr = *(pBlock + 1);
                        fixed(byte* p = &obj.GetRawData())
                        {
                            MemCpy(p, (byte*)pPreInitDataAddr, obj.GetRawDataSize());
                        }
                    }

                    var handle = malloc((ulong)sizeof(IntPtr));
                    *(IntPtr*)handle = Unsafe.As<object, IntPtr>(ref obj);
                    *pBlock = handle;
                }
            }
        }
    }
}
