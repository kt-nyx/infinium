# Evaluator history

Status: Accepted
Disposition: Compact archive and authority pointer

Last reviewed: 2026-08-25

Historical evaluator attempts, plans, incident chronology, proof fixtures, and
provider-development staging are not current product inputs. Their detailed
records were consolidated during the post-M1 cleanup in
`../infinium-development-history-archive/` commit
`6f8976db6c560456201a9166caf4f36506be5477`.

The current rules are:

- protocol `/4` is retired and preserved separately in the immutable
  `../infinium-evaluator-archive/` repository;
- protocol `/5` is retired without a valid product verdict;
- private held-out evaluation is deferred and the private fixture repository
  remains default-deny for ordinary product work;
- independent semantic-oracle qualification is deferred through M2 by
  [ADR-0035](../architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md);
- current public fixtures provide developer-owned product-conformance evidence,
  not an independent semantic verdict; and
- no historical namespace, path, record, package, or Git identity grants current
  runtime or evaluation authority.

Consult the [product/evaluator boundary](product-evaluator-boundary.md) and its
machine-readable authority inventory for current repository rules. Inspect the
archive only when the project owner explicitly authorizes archaeological
review.
