# Site Access Form-to-Field Lineage Design

## Placement

- Article: `/blog/access-form-to-field-lineage/`
- Companion: `/blog/reverse-engineering-access-without-running-it/`

The article and companion link bidirectionally. The build generates the route
and sitemap entry. No new primary-navigation item or public proof packet is
added.

## Evidence design

The article presents a representative categorical trail assembled from shipped
rule and documentation contracts. It does not invent fixture rows or publish
protected input. Each hop is described by its own tier, and the path remains
bounded by the weakest required hop and any gap.

## Stack design

This branch is stacked on PR #625 because its companion route is not yet in
`dev`. The PR targets `codex/site-access-safe-acquisition` until the base PR is
merged, after which it can be retargeted to `dev` without changing content.
