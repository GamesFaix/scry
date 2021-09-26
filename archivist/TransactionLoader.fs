﻿module GamesFaix.MtgTools.Archivist.TransactionLoader

open Serilog
open Model

let private loadCardFile (name: string) (dir: Workspace.TransactionDirectory) (log: ILogger): CardCount list Async =
    async {
        let path = dir.GetCardFile name
        log.Information $"\tLoading cards from {path}..."
        let! cards = Csv.loadCardFile path
        log.Information $"\tFound {cards.Length} cards."
        return cards
    }

let private collectAsync<'a, 'b> (projection: 'a -> 'b list Async) (source: 'a list) : 'b list Async =
    async {
        let results = ResizeArray()
        for x in source do
            let! ys = projection x
            results.AddRange ys
        return results |> Seq.toList
    }

let loadTransactionDetails (dir: Workspace.TransactionDirectory) (log: ILogger) : Result<TransactionDetails, string> Async =
    async {
        let! maybeManifest = FileSystem.loadFromJson<TransactionManifest> dir.Manifest
        match maybeManifest with
        | None -> return Error "\tTransaction manifest not found."
        | Some manifest ->
            let! add = 
                manifest.AddFiles 
                |> Option.defaultValue [] 
                |> collectAsync (fun f -> loadCardFile f dir log)

            let! subtract =
                manifest.SubtractFiles 
                |> Option.defaultValue []
                |> collectAsync (fun f -> loadCardFile f dir log)

            log.Information $"\tAdding {add.Length} and subtracting {subtract.Length} cards."
            return Ok {
                Info = manifest.Info
                Add = add
                Subtract = subtract
            }
    }