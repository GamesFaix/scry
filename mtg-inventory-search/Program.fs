﻿module GamesFaix.MtgInventorySearch.Program

open System
open System.Collections.Generic
open System.Linq
open System.Text
open Microsoft.Extensions.Configuration
open GamesFaix.MtgInventorySearch.Inventory
open System.Text.RegularExpressions
type ScryfallCard = ScryfallApi.Client.Models.Card

type Settings = {
    Query: string // https://scryfall.com/docs/syntax
    InventoryPath: string
}

let configure args =
    let argMap = Dictionary<string, string>()
    argMap.Add("-i", "InventoryPath")
    argMap.Add("--inventory", "InventoryPath")
    argMap.Add("-q", "Query")
    argMap.Add("--query", "Query")

    let config =
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = false)
            .AddCommandLine(args, argMap)
            .Build()

    let settings = {
        Query = config.["Query"]
        InventoryPath = Environment.ExpandEnvironmentVariables(config.["InventoryPath"])
    }

    if String.IsNullOrWhiteSpace(settings.InventoryPath) then failwith $"{nameof(settings.InventoryPath)} cannot be blank"
    if String.IsNullOrWhiteSpace(settings.Query) then failwith $"{nameof(settings.Query)} cannot be blank"

    settings

let joinResults (scryfallResults: ScryfallCard list) (fullInventory: Inventory.Card list) =
    Enumerable.Join(
        scryfallResults,
        fullInventory,
        (fun x -> x.Name.ToLowerInvariant()),
        (fun y -> y.Name.ToLowerInvariant()),
        (fun x y -> (x, y))
    )
    |> Seq.toList

let formatCardOutput (scryfallCard: ScryfallCard, inventoryCard: Inventory.Card) : string =
    let name = scryfallCard.Name.PadRight(30)
    let typeline = scryfallCard.TypeLine.PadRight(25)
    let cost =
        Regex.Replace(scryfallCard.ManaCost, "{(\\d|W|U|B|R|G)}", "$1")
             .PadLeft(6)
    let count = $"(x{inventoryCard.Count})".PadLeft(5)

    let sb = StringBuilder()
    sb.AppendLine $"{name} {typeline} {cost} {count}" |> ignore

    for e in inventoryCard.Editions do
        let set = e.Set
        let count = $"(x{e.Count})".PadLeft(5)
        sb.AppendLine $"  {set} {count}" |> ignore

    sb.ToString()

[<EntryPoint>]
let main args =
    async {
        let settings = configure args

        printfn $"Searching Scryfall for \"{settings.Query}\"..."
        let! scryfallResults = Scryfall.search settings.Query
        printfn $"  Found {scryfallResults.Length} results"

        printfn $"Loading inventory from {settings.InventoryPath}..."
        let fullInventory = Inventory.load settings.InventoryPath
        printfn "  Found %i distinct cards, %i editions, and %i total cards."
            fullInventory.Length
            (fullInventory |> Seq.sumBy (fun c -> c.Editions.Length))
            (fullInventory |> Seq.sumBy (fun c -> c.Count))

        printfn "Joining search results with inventory..."
        let joined = joinResults scryfallResults fullInventory
        printfn "  Found %i distinct cards, %i editions, and %i total cards."
            joined.Length
            (joined |> Seq.sumBy (fun (_, c) -> c.Editions.Length))
            (joined |> Seq.sumBy (fun (_, c) -> c.Count))

        printfn ""

        for c in joined do
            printfn "%s" (formatCardOutput c)

        return 0 // return an integer exit code
    } |> Async.RunSynchronously

