using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XBase.Abstractions;

namespace XBase.Core.Transactions;

public sealed class WalRecoveryResult
{
  public static WalRecoveryResult Empty { get; } = new(
    Array.Empty<WalCommittedTransaction>(),
    Array.Empty<WalInFlightTransaction>(),
    hasChecksumFailure: false,
    hasTruncatedTail: false);

  public WalRecoveryResult(
    IReadOnlyList<WalCommittedTransaction> committedTransactions,
    IReadOnlyList<WalInFlightTransaction> incompleteTransactions,
    bool hasChecksumFailure,
    bool hasTruncatedTail)
  {
    CommittedTransactions = new ReadOnlyCollection<WalCommittedTransaction>(
      committedTransactions is List<WalCommittedTransaction> committedList
        ? committedList
        : new List<WalCommittedTransaction>(committedTransactions));
    IncompleteTransactions = new ReadOnlyCollection<WalInFlightTransaction>(
      incompleteTransactions is List<WalInFlightTransaction> incompleteList
        ? incompleteList
        : new List<WalInFlightTransaction>(incompleteTransactions));
    HasChecksumFailure = hasChecksumFailure;
    HasTruncatedTail = hasTruncatedTail;
  }

  public IReadOnlyList<WalCommittedTransaction> CommittedTransactions { get; }

  public IReadOnlyList<WalInFlightTransaction> IncompleteTransactions { get; }

  public bool HasChecksumFailure { get; }

  public bool HasTruncatedTail { get; }
}

public sealed record WalCommittedTransaction(
  long TransactionId,
  DateTimeOffset BeganAt,
  DateTimeOffset CommittedAt,
  IReadOnlyList<JournalMutation> Mutations);

public sealed record WalInFlightTransaction(
  long TransactionId,
  DateTimeOffset BeganAt,
  bool HasRollbackMarker,
  IReadOnlyList<JournalMutation> Mutations);
