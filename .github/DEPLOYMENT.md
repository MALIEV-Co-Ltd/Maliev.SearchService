# SearchService image promotion

SearchService builds a deployable container exactly once on `develop`. Staging and production copy the verified OCI digest between Artifact Registry repositories; they never rebuild source.

## Required GitHub configuration

The repository uses these GitHub Environments:

| Environment | Allowed ref | Approval |
|---|---|---|
| `development` | `develop` branch | No manual approval |
| `staging` | `release/v*` tags | Core Developers; self-review blocked |
| `production` | `main` branch | Core Developers; self-review blocked |

The following secrets must be available to every applicable environment, either directly or through an organization secret explicitly shared with this repository:

- `GCP_WORKLOAD_IDENTITY_PROVIDER`: full Workload Identity Provider resource name.
- `GCP_SERVICE_ACCOUNT`: least-privilege deployment service-account email.
- `GITOPS_PAT`: GitHub credential limited to reading MALIEV packages and creating branches and draft pull requests in `maliev-gitops`.

Do not configure `GCP_SA_KEY` or another long-lived Google service-account JSON credential.

The WIF attribute condition must restrict trust to `MALIEV-Co-Ltd/Maliev.SearchService`. Grant only Artifact Registry read/write access required by the environment's source and target repositories.

## Promotion chain

1. Pull requests build and smoke-test the production image locally, emit a CycloneDX SBOM, and block HIGH or CRITICAL vulnerabilities.
2. `develop` publishes `dev-{shortSHA}`, records its registry digest, emits provenance/SBOM evidence, and opens a draft `[DO NOT MERGE]` development GitOps pull request.
3. `release/v*` resolves that exact develop digest, verifies its GitHub attestation, copies it to staging, and requires digest equality.
4. `main` (or an approved manual production promotion) resolves the staging digest, verifies the original attestation, copies it to production, and requires digest equality.

All GitOps pull requests update only `3-apps/maliev-search-service/overlays/*`. They remain draft evidence. These workflows do not enable an Argo CD Application, merge GitOps changes, sync a cluster, or deploy a workload.
