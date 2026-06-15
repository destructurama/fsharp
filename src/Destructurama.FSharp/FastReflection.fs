namespace Destructurama.FSharp

open System
open System.Reflection
open System.Reflection.Emit
open Microsoft.FSharp.Reflection

/// High-performance reflection utilities using System.Reflection.Emit
module FastReflection =
    
    /// Caches for generated delegates
    module private Cache =
        let recordConstructors = System.Collections.Concurrent.ConcurrentDictionary<Type, obj[] -> obj>()
        let recordDeconstructors = System.Collections.Concurrent.ConcurrentDictionary<Type, obj -> obj[]>()
        let unionConstructors = System.Collections.Concurrent.ConcurrentDictionary<Type * string, obj[] -> obj>()
        let unionDeconstructors = System.Collections.Concurrent.ConcurrentDictionary<Type, obj -> (string * obj[])>()
        let unionTagReader = System.Collections.Concurrent.ConcurrentDictionary<Type, obj -> int>()
        let unionCaseInfos = System.Collections.Concurrent.ConcurrentDictionary<Type, UnionCaseInfo[]>()
    
    /// Fast record operations
    module FastRecord =
        
        /// Creates a record constructor delegate for the given type
        let getConstructor (recordType: Type) : obj[] -> obj =
            Cache.recordConstructors.GetOrAdd(recordType, fun rt ->
                try
                    let fields = FSharpType.GetRecordFields(rt, BindingFlags.Public ||| BindingFlags.NonPublic)
                    let fieldCount = fields.Length
                    
                    let dynamicMethod = DynamicMethod(
                        sprintf "ConstructRecord_%s" rt.FullName,
                        typeof<obj>,
                        [| typeof<obj[]> |],
                        typeof<obj>.Module,
                        true)
                    
                    let il = dynamicMethod.GetILGenerator()
                    
                    // Create new instance
                    let ctor = rt.GetConstructor(BindingFlags.Instance ||| BindingFlags.NonPublic ||| BindingFlags.Public, null, fields |> Array.map (fun f -> f.PropertyType), null)
                    
                    if ctor <> null then
                        il.Emit(OpCodes.Ldarg_0)
                        il.Emit(OpCodes.Ldc_I4, fieldCount)
                        il.Emit(OpCodes.Newarr, typeof<obj>)
                        il.Emit(OpCodes.Stloc_0)
                        
                        // Load each argument from the array and box if necessary
                        for i = 0 to fieldCount - 1 do
                            il.Emit(OpCodes.Ldarg_0)
                            il.Emit(OpCodes.Ldc_I4, i)
                            il.Emit(OpCodes.Ldelem_Ref)
                            let fieldType = fields.[i].PropertyType
                            if fieldType.IsValueType then
                                il.Emit(OpCodes.Unbox_Any, fieldType)
                            il.Emit(OpCodes.Stloc_S, byte (i + 1))
                        
                        // Call constructor
                        il.Emit(OpCodes.Ldloc_0)
                        il.Emit(OpCodes.Newobj, ctor)
                        il.Emit(OpCodes.Box, rt)
                        il.Emit(OpCodes.Ret)
                        
                        dynamicMethod.CreateDelegate(typeof<Func<obj[], obj>>) :?> Func<obj[], obj>
                        |> fun f -> f.Invoke
                    else
                        // Fallback to FSharp reflection
                        let ctor = FSharpValue.PreComputeRecordConstructor(rt, BindingFlags.Public ||| BindingFlags.NonPublic)
                        fun args -> ctor args
                with
                | _ -> 
                    let ctor = FSharpValue.PreComputeRecordConstructor(rt, BindingFlags.Public ||| BindingFlags.NonPublic)
                    fun args -> ctor args
            )
        
        /// Creates a record deconstructor delegate for the given type
        let getDeconstructor (recordType: Type) : obj -> obj[] =
            Cache.recordDeconstructors.GetOrAdd(recordType, fun rt ->
                try
                    let fields = FSharpType.GetRecordFields(rt, BindingFlags.Public ||| BindingFlags.NonPublic)
                    let fieldCount = fields.Length
                    
                    let dynamicMethod = DynamicMethod(
                        sprintf "DeconstructRecord_%s" rt.FullName,
                        typeof<obj[]>,
                        [| typeof<obj> |],
                        typeof<obj>.Module,
                        true)
                    
                    let il = dynamicMethod.GetILGenerator()
                    
                    // Create array for results
                    il.Emit(OpCodes.Ldc_I4, fieldCount)
                    il.Emit(OpCodes.Newarr, typeof<obj>)
                    il.Emit(OpCodes.Stloc_0)
                    
                    // Load each field value
                    for i = 0 to fieldCount - 1 do
                        il.Emit(OpCodes.Ldloc_0)
                        il.Emit(OpCodes.Ldc_I4, i)
                        il.Emit(OpCodes.Ldarg_0)
                        il.Emit(OpCodes.Unbox_Any, rt)
                        il.Emit(OpCodes.Callvirt, fields.[i].GetGetMethod(true))
                        let fieldType = fields.[i].PropertyType
                        if fieldType.IsValueType then
                            il.Emit(OpCodes.Box, fieldType)
                        il.Emit(OpCodes.Stelem_Ref)
                    
                    il.Emit(OpCodes.Ldloc_0)
                    il.Emit(OpCodes.Ret)
                    
                    dynamicMethod.CreateDelegate(typeof<Func<obj, obj[]>>) :?> Func<obj, obj[]>
                    |> fun f -> f.Invoke
                with
                | _ ->
                    let reader = FSharpValue.PreComputeRecordReader(rt, BindingFlags.Public ||| BindingFlags.NonPublic)
                    fun obj -> reader obj
            )
    
    /// Fast union operations
    module FastUnion =
        
        /// Gets all union cases for a type
        let getUnionCases (unionType: Type) : UnionCaseInfo[] =
            Cache.unionCaseInfos.GetOrAdd(unionType, fun ut ->
                FSharpType.GetUnionCases(ut, BindingFlags.Public ||| BindingFlags.NonPublic)
            )
        
        /// Creates a union constructor delegate for the given case
        let getConstructor (unionType: Type, caseName: string) : obj[] -> obj =
            Cache.unionConstructors.GetOrAdd((unionType, caseName), fun (ut, cn) ->
                try
                    let cases = getUnionCases ut
                    let caseInfo = cases |> Array.find (fun c -> c.Name = cn)
                    let fields = caseInfo.GetFields()
                    let fieldCount = fields.Length
                    
                    let dynamicMethod = DynamicMethod(
                        sprintf "ConstructUnion_%s_%s" ut.FullName cn,
                        typeof<obj>,
                        [| typeof<obj[]> |],
                        typeof<obj>.Module,
                        true)
                    
                    let il = dynamicMethod.GetILGenerator()
                    
                    // Load each argument
                    for i = 0 to fieldCount - 1 do
                        il.Emit(OpCodes.Ldarg_0)
                        il.Emit(OpCodes.Ldc_I4, i)
                        il.Emit(OpCodes.Ldelem_Ref)
                        let fieldType = fields.[i].PropertyType
                        if fieldType.IsValueType then
                            il.Emit(OpCodes.Unbox_Any, fieldType)
                        il.Emit(OpCodes.Stloc_S, byte i)
                    
                    // Call the union case constructor
                    let ctor = caseInfo.GetConstructor()
                    il.Emit(OpCodes.Newobj, ctor)
                    il.Emit(OpCodes.Box, ut)
                    il.Emit(OpCodes.Ret)
                    
                    dynamicMethod.CreateDelegate(typeof<Func<obj[], obj>>) :?> Func<obj[], obj>
                    |> fun f -> f.Invoke
                with
                | _ ->
                    let ctor = FSharpValue.PreComputeUnionConstructor(caseInfo, BindingFlags.Public ||| BindingFlags.NonPublic)
                    fun args -> ctor args
            )
        
        /// Creates a union deconstructor delegate for the given type
        let getDeconstructor (unionType: Type) : obj -> (string * obj[]) =
            Cache.unionDeconstructors.GetOrAdd(unionType, fun ut ->
                try
                    let cases = getUnionCases ut
                    let tagReader = getTagReader ut
                    
                    let dynamicMethods = 
                        cases |> Array.map (fun case ->
                            let fields = case.GetFields()
                            let fieldCount = fields.Length
                            
                            let dynamicMethod = DynamicMethod(
                                sprintf "DeconstructUnion_%s_%s" ut.FullName case.Name,
                                typeof<obj[]>,
                                [| typeof<obj> |],
                                typeof<obj>.Module,
                                true)
                            
                            let il = dynamicMethod.GetILGenerator()
                            
                            // Create array for results
                            il.Emit(OpCodes.Ldc_I4, fieldCount)
                            il.Emit(OpCodes.Newarr, typeof<obj>)
                            il.Emit(OpCodes.Stloc_0)
                            
                            // Load each field value
                            for i = 0 to fieldCount - 1 do
                                il.Emit(OpCodes.Ldloc_0)
                                il.Emit(OpCodes.Ldc_I4, i)
                                il.Emit(OpCodes.Ldarg_0)
                                il.Emit(OpCodes.Unbox_Any, ut)
                                il.Emit(OpCodes.Callvirt, fields.[i].GetGetMethod(true))
                                let fieldType = fields.[i].PropertyType
                                if fieldType.IsValueType then
                                    il.Emit(OpCodes.Box, fieldType)
                                il.Emit(OpCodes.Stelem_Ref)
                            
                            il.Emit(OpCodes.Ldloc_0)
                            il.Emit(OpCodes.Ret)
                            
                            dynamicMethod.CreateDelegate(typeof<Func<obj, obj[]>>) :?> Func<obj, obj[]>
                        )
                    
                    fun obj ->
                        let tag = tagReader obj
                        let case = cases.[tag]
                        let values = dynamicMethods.[tag].Invoke(obj)
                        (case.Name, values)
                with
                | _ ->
                    let reader = FSharpValue.PreComputeUnionReader(ut, BindingFlags.Public ||| BindingFlags.NonPublic)
                    let tagReader = FSharpValue.PreComputeUnionTagReader(ut, BindingFlags.Public ||| BindingFlags.NonPublic)
                    let cases = getUnionCases ut
                    fun obj ->
                        let tag = tagReader obj
                        let case = cases.[tag]
                        let values = reader obj
                        (case.Name, values)
            )
        
        /// Creates a union tag reader delegate for the given type
        let getTagReader (unionType: Type) : obj -> int =
            Cache.unionTagReader.GetOrAdd(unionType, fun ut ->
                try
                    let dynamicMethod = DynamicMethod(
                        sprintf "GetUnionTag_%s" ut.FullName,
                        typeof<int>,
                        [| typeof<obj> |],
                        typeof<obj>.Module,
                        true)
                    
                    let il = dynamicMethod.GetILGenerator()
                    
                    // Get the Tag property
                    let tagProp = ut.GetProperty("Tag", BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                    if tagProp <> null then
                        il.Emit(OpCodes.Ldarg_0)
                        il.Emit(OpCodes.Unbox_Any, ut)
                        il.Emit(OpCodes.Callvirt, tagProp.GetGetMethod(true))
                        il.Emit(OpCodes.Ret)
                    else
                        // Fallback to FSharp reflection
                        let tagReader = FSharpValue.PreComputeUnionTagReader(ut, BindingFlags.Public ||| BindingFlags.NonPublic)
                        il.Emit(OpCodes.Ldarg_0)
                        il.Emit(OpCodes.Call, tagReader.GetType().GetMethod("Invoke"))
                        il.Emit(OpCodes.Ret)
                    
                    dynamicMethod.CreateDelegate(typeof<Func<obj, int>>) :?> Func<obj, int>
                    |> fun f -> f.Invoke
                with
                | _ ->
                    let tagReader = FSharpValue.PreComputeUnionTagReader(ut, BindingFlags.Public ||| BindingFlags.NonPublic)
                    fun obj -> tagReader obj
            )