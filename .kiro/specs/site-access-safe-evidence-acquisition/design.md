# Site Access Safe Evidence Acquisition Design

## Placement

- Article: `/blog/reverse-engineering-access-without-running-it/`
- Registry: `site/src/_blog/articles.json`
- Discovery: `site/src/_site/discovery.json`

The build owns the rendered route and sitemap entry. No generated directory is
edited. The article uses the existing blog layout and avoids new primary
navigation.

## Claim design

The article uses `demo` because its behavioral statements are grounded in
checked-in documentation and rule-catalog contracts. It publishes no proof
packet or protected Access artifact. Acquisition controls remain bounded
static-evidence behavior; they do not become claims about representative files,
runtime behavior, reconstruction, completeness, correctness, or safety.

## Validation design

A focused validator pins the article route, metadata, blocks, rule IDs, links,
discovery entry, sitemap entry, claim level, and boundary wording. Negative
tests plant runtime/reconstruction overclaims and private or executable
material. The full site validators continue to check references and discovery.
