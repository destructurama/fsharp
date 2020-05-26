// Copyright 2015 Destructurama Contributors, Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Destructurama.FSharp

open Microsoft.FSharp.Reflection
open Serilog.Core
open Serilog.Events
open System

type public FSharpTypesDestructuringPolicy() =
    interface Serilog.Core.IDestructuringPolicy with
        member __.TryDestructure(value,
                                 propertyValueFactory : ILogEventPropertyValueFactory,
                                 result: byref<LogEventPropertyValue>) =
            let cpv obj = propertyValueFactory.CreatePropertyValue(obj, true)
            let lep (n:System.Reflection.PropertyInfo) (v:obj) = LogEventProperty(n.Name, cpv v)
            match value.GetType() with
            | t when FSharpType.IsTuple t ->
                result <- SequenceValue(value |> FSharpValue.GetTupleFields |> Seq.map cpv)
                true
            // TODO: support for Maps and Sets? Why do Lists here when surely some IEnumerable-binder can handle them?
            | t when t.IsConstructedGenericType && t.GetGenericTypeDefinition() = typedefof<List<_>> ->
                let objEnumerable = value :?> System.Collections.IEnumerable |> Seq.cast<obj>
                result <- SequenceValue(objEnumerable |> Seq.map cpv)
                true
            | t when FSharpType.IsUnion t ->
                let case, fields = FSharpValue.GetUnionFields(value, t)
                let properties = (case.GetFields(), fields) ||> Seq.map2 lep
                result <- StructureValue(properties, case.Name)
                true
            | t when FSharpType.IsRecord t ->
                let fields = FSharpValue.GetRecordFields value
                let fieldNames = FSharpType.GetRecordFields t
                let properties = (fieldNames, fields) ||> Seq.map2 lep
                result <- StructureValue(properties, t.Name)
                true
            | _ -> false

namespace Serilog

open Serilog.Configuration
open Destructurama.FSharp

[<AutoOpen>]
module public LoggerDestructuringConfigurationExtensions =
    type public LoggerDestructuringConfiguration with
        member public this.FSharpTypes() =
            this.With<FSharpTypesDestructuringPolicy>()

