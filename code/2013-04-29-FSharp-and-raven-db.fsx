(*** raw ***)
---
layout: page
title: F# and Raven DB
---

(*** hide ***)
#r @"..\packages\RavenDB.Server\tools\Raven.Client.Lightweight.dll"
#r @"..\packages\RavenDB.Server\tools\Raven.Abstractions.dll"
open System
open Raven.Client
open Raven.Client.Document
open Raven.Imports.Newtonsoft.Json

module Json = 
    
    open System
    open System.Collections.Generic
    open System.Reflection
    open Microsoft.FSharp.Reflection
    open Raven.Imports.Newtonsoft.Json
    open System.Text
    open System.IO
    
    type UnionTypeConverter() =
        inherit JsonConverter()
    
        let cache = new Dictionary<_,_>()
    
        let memoize f =
            fun x ->
                if cache.ContainsKey(x) then cache.[x]
                else let res = f x
                     cache.[x] <- res
                     res
    
        let doRead pos (reader: JsonReader) = 
            reader.Read() |> ignore 
    
        override x.CanConvert(t:Type) =
            let result = 
                memoize (fun (typ:Type) ->
                            ((typ.GetInterface(typeof<System.Collections.IEnumerable>.FullName) = null) 
                            && FSharpType.IsUnion typ)
                        )
            result t
    
        override x.WriteJson(writer: JsonWriter, value: obj, serializer: JsonSerializer) =
            let t = value.GetType()
            let write (name : string) (fields : obj []) = 
                writer.WriteStartObject()
                writer.WritePropertyName("case")
                writer.WriteValue(name)  
                writer.WritePropertyName("values")
                serializer.Serialize(writer, fields)
                writer.WriteEndObject()   
    
            let (info, fields) = FSharpValue.GetUnionFields(value, t)
            write info.Name fields
    
        override x.ReadJson(reader: JsonReader, objectType: Type, existingValue: obj, serializer: JsonSerializer) =      
             let cases = FSharpType.GetUnionCases(objectType)
             if reader.TokenType <> JsonToken.Null  
             then 
                doRead "1" reader
                doRead "2" reader
                let case = cases |> Array.find(fun x -> x.Name = if reader.Value = null then "None" else reader.Value.ToString())
                doRead "3" reader
                doRead "4" reader
                doRead "5" reader
                let fields =  [| 
                       for field in case.GetFields() do
                           let result = serializer.Deserialize(reader, field.PropertyType)
                           reader.Read() |> ignore
                           yield result
                 |] 
                let result = FSharpValue.MakeUnion(case, fields)
                while reader.TokenType <> JsonToken.EndObject do
                    doRead "6" reader         
                result
             else
                FSharpValue.MakeUnion(cases.[0], [||])
    
    type MapTypeConverter() =
        inherit JsonConverter()
            
        let doRead (reader:JsonReader) = 
            reader.Read() |> ignore
    
        let readKeyValuePair (serializer:JsonSerializer) (argTypes:Type []) (reader:JsonReader) =
            doRead reader // "key"
            doRead reader // value
            let key = serializer.Deserialize(reader, argTypes.[0])
            doRead reader // "value"
            doRead reader // value
            let value = serializer.Deserialize(reader, argTypes.[1])
            doRead reader // }
            FSharpValue.MakeTuple([|key;value|], FSharpType.MakeTupleType(argTypes))
    
        let readArray elementReaderF (reader:JsonReader) =
            doRead reader // [
            if reader.TokenType = JsonToken.StartArray then
                [|
                    while reader.TokenType <> JsonToken.EndArray do
                        doRead reader // {
                        if reader.TokenType = JsonToken.StartObject then
                            let element = elementReaderF reader
                            yield element
                |]
            else
                Array.empty
    
        let writeKeyValuePair (serializer:JsonSerializer) (writer:JsonWriter) kv =
            let kvType = kv.GetType()
            let k = kvType.GetProperty("Key").GetValue(kv, null)
            let v = kvType.GetProperty("Value").GetValue(kv, null)
            writer.WriteStartObject()
            writer.WritePropertyName("key")
            serializer.Serialize(writer,k)
            writer.WritePropertyName("value")
            serializer.Serialize(writer,v)
            writer.WriteEndObject()
    
        let writeArray elementWriterF (writer:JsonWriter) (kvs:System.Collections.IEnumerable) =
            writer.WriteStartArray()
            for kv in kvs do
                elementWriterF writer kv
            writer.WriteEndArray()
                            
        override x.CanConvert(typ:Type) =
            typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<Map<_,_>>
    
        override x.WriteJson(writer:JsonWriter, value:obj, serializer:JsonSerializer) =
            if value <> null then
                let valueType = value.GetType()
                if valueType.IsGenericType then
                    let baseType = valueType.GetGenericTypeDefinition()
                    if baseType = typedefof<Map<_,_>> then
                        let kvs = value :?> System.Collections.IEnumerable
                        writer.WriteStartObject()
                        writer.WritePropertyName("pairs")
                        writeArray (writeKeyValuePair serializer) writer kvs
                        writer.WriteEndObject()   
    
        override x.ReadJson(reader:JsonReader, objectType:Type, existingValue:obj, serializer:JsonSerializer) =
            let argTypes = objectType.GetGenericArguments()
            let tupleType = FSharpType.MakeTupleType(argTypes)
            let constructedIEnumerableType = typedefof<IEnumerable<_>>.GetGenericTypeDefinition().MakeGenericType(tupleType)
            if reader.TokenType <> JsonToken.Null then
                doRead reader // "pairs"
                let kvs = readArray (readKeyValuePair serializer argTypes) reader
                doRead reader // }
                let kvs' = System.Array.CreateInstance(tupleType, kvs.Length)
                System.Array.Copy(kvs, kvs', kvs.Length)
                let methodInfo = objectType.GetMethod("Create", BindingFlags.Static ||| BindingFlags.NonPublic, null, [|constructedIEnumerableType|], null)
                methodInfo.Invoke(null, [|kvs'|])
            else
                box Map.empty
    
(*** define:blogtype ***)
type BlogPost = {
    mutable Id : string
    Title : string
    Body : string
    Published : DateTime
}


(**
Ok so I have blogged about these to before. Previously, F# didn't have brilliant support for Linq. F# Expressions couldn't `easily` be 
converted to the Linq expression trees that the RavenDB API required. This caused somewhat of a mis-match between the two which made 
Raven difficult but not impossible to use from F#. My previous blog introduced a library  which is available for Raven clients prior 
to version 2.0, to bridge this gap, and tried to make Raven more natural to use from F#. However as of Raven 2.0 this library has 
been removed. The reasons are explained [here](https://groups.google.com/forum/?fromgroups=#!searchin/ravendb/fsharp/ravendb/Zv3bwF4Y07M/qsCDbP9VKKkJ). I don't 
disagree with the reasons ayende cut the support, I wouldn't want to support something I had little knowledge of either. 
However things have changed.... :)

##The advent F# 3 and query expressions

So we are now in the era of F# 3.0 and things have changed somewhat. F# is now truly in the data era... Information Rich programming is an 
awesome feature of F# manifested in the form of `Type Providers` and `Query Expressions`. If you haven't read about or don't know what 
type providers are then I encourage you to check them out [here](http://msdn.microsoft.com/en-us/library/hh156509.aspx). Type providers are 
not really applicable for use with RavenDB see it is schemaless so for the rest of thispost we will focus on `Query Expressions`. It is this 
feature that means the gap between Linq and F# no longer exists. If you are familiar with 

    [lang=csharp]
    var result = 
        (from x in xs do
         where x.Published >= someDate
         select x.Name).ToArray()

				 
then the query expression syntax shouldn't feel that different,
*)

let publishedOn date xs =
    query {
           for x in xs do
           where (x.Published >= date)
           select x.Title
      } |> Seq.toArray

(**

##So What about RavenDB?

Using RavenDB from C# is well documented and things are not that different when using it from F#. The in's and out's are well known and lets 
face it the API is your safety net. It doesn't let you kill yourself, in fact you have to try very hard to actually do anything really bad in 
RavenDB. This is I think the best feature of RavenDB.

So, what are the things that we need to be aware of when using RavenDB from F#? First things first, initializing the document store. This can 
be done pretty much the same as in C# 
*)


let store = new DocumentStore(Url = "http://localhost:8080")

(**
and this is all well and good if we just use straight forward POCO objects. But what if we want to use F# record or Union types? We need to make a few simple changes.
Lets first consider F# record types, all we need to do here is declare the `Id` property as mutable.
*)

(*** include:blogtype ***)

(**
Simple eh?, but what about Union types, Maps, F# lists? These are a little more complex as Json.NET doesn't do the correct thing to serialize these out of the box. However Json.NET and the 
internalised Raven counterpart does have an extension point and a UnionTypeConverter or MapTypeConverter as Json.NET implementations can be found [here](https://github.com/colinbull/FSharp.Enterprise/blob/develop/src/FSharp.Enterprise/Json.fs). To use this we need to modify our
document store setup a little to include the converter. 
*)

let customisedStore =
    let customiseSerialiser (s : Raven.Imports.Newtonsoft.Json.JsonSerializer) =
        s.Converters.Add(new Json.MapTypeConverter())
        s.Converters.Add(new Json.UnionTypeConverter())

    let store = new DocumentStore(Url="http://localhost:8080")
    store.Conventions.CustomizeJsonSerializer <- (fun s -> (customiseSerialiser s))
    store.Initialize()

(**
##Querying raven
	
With the document store setup we can now start querying Raven. As with any Raven query we need to create a session from our document store, 
then we need to create the query.

*)

let getPostsAsOf asOfDate = 
    use session = customisedStore.OpenSession()
    query {
        for post in session.Query<BlogPost>() do
        where (post.Published >= asOfDate)
        select post
    }

(**		
The above creates a query that is pending execution. To execute this we can run, 
*)

let posts = 
    getPostsAsOf (DateTime.Now.AddDays(-2.).Date) 
    |> Seq.toArray

(**
this will give us an array of BlogPosts published from 2 days ago. Notice we had to enumerate to an array for the query to actually be executed. This is the same execution semantics
as C#; and it is important to realise that there isn't really any magic going on in the world of F#, it is still just a .NET lanuguage it still compiles to CIL and is fully
interoperable with any other .NET library.


##Indexes

OK so things haven't got much better here in terms of static indexes. Basically you still need to define them in C# and then you can extend the document initialization process by
including the assembly that contains the index definitions. However in Raven 2.0, dynamic indexes and promotion to permanent indexes have been massively improved, which reduces the need to create static indexes.
*)