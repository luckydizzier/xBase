# xBase for .NET — Transactions & Journaling (TRANSACTIONS.md)

## Transaction Model
- **Scope** — Supports explicit (`BEGIN TRANSACTION`) and ambient (`TransactionScope`) units of work across tables and memo stores.
- **Isolation** — Snapshot reads with write-intent locking. Record-level locks optional; file locks enforced for shared resources.
- **Durability** — Write-Ahead Log (WAL) persisted before data/index flush. Checkpoints roll up committed pages and prune log tails.

## Journaling Pipeline
```mermaid
digraph JournalFlow {
  rankdir=LR
  Client[Client Command]
  Parser[Command Parser]
  Planner[Mutation Planner]
  Journal[WAL Append]
  DataFlush[Data File Flush]
  IndexFlush[Index Update]
  Checkpoint[Checkpoint]

  Client -> Parser -> Planner -> Journal -> DataFlush -> IndexFlush -> Checkpoint;
  Journal -> Checkpoint [label="LSN"];
  Checkpoint -> Journal [label="Truncate"];
}
```

## Crash Recovery Stages
1. **Discovery** — Locate latest durable checkpoint metadata.
2. **Redo** — Replay committed entries > checkpoint LSN.
3. **Undo** — Roll back in-flight transactions using compensation records.
4. **Rebuild** — Resubmit deferred index operations flagged before crash.

## Configuration Surface
| Channel | Setting | Description | Default |
|---------|---------|-------------|---------|
| Connection string | `JournalMode` | `Sync`, `Async`, or `Off` (read-only) | `Sync` |
| Connection string | `JournalPath` | Override default `.journal` co-location | Adjacent to DBF |
| Options API | `UseTransactions(bool)` | Disable implicit transactions for batch loads | `true` |
| CLI | `xbase transactions verify` | Validates journal health and checkpoints | n/a |

## Testing Matrix
- **Unit** — `JournalWriterTests`, `TransactionCoordinatorTests` cover WAL layout, concurrency, and compensating actions.
- **Integration** — `TransactionRecoveryTests` simulate crash + recovery across Windows/Linux runners.
- **Stress** — Long-running soak tests executed nightly via CI matrix (4h) to validate log rollover, checkpoint cadence, and disk pressure.

## Operational Playbook
- Monitor `JournalLagBytes` metric. Trigger `xbase transactions checkpoint` if lag exceeds 512 MiB.
- On disk-full conditions, enter `ReadOnly` mode and surface health alerts.
- Expose diagnostic dump via `xbase transactions dump --lsn-range <start>:<end>` for support analysis.

**End of TRANSACTIONS.md**
