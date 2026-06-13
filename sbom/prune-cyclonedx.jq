# Prunes the CycloneDX BOM produced by dotnet-CycloneDX so it accurately
# reflects the published package's runtime dependency footprint.
#
# Rationale: with --exclude-dev the tool drops packages flagged as
# DevelopmentDependency (analyzers, SourceLink, Polyfill) but leaves their
# transitive dependencies behind as orphan components that are no longer
# reachable from the root component. For EdsDcfNet the published NuGet package
# declares zero runtime dependencies, so these orphans (System.*, SourceLink
# internals, …) misrepresent the supply chain for SCA tooling.
#
# This filter keeps only components reachable from the root component via the
# dependency graph, prunes dangling dependency entries, and adds a truthful
# `compositions` block asserting the dependency inventory is complete. It uses
# no hardcoded package names, so genuine runtime dependencies added later are
# retained automatically.

def closure($adj):
  def step(s): (s + ([ s[] | ($adj[.] // []) ] | add // [])) | unique;
  until(step(.) == .; step(.));

. as $bom
| ($bom.metadata.component["bom-ref"]) as $root
| (reduce ($bom.dependencies // [])[] as $d ({}; .[$d.ref] = ($d.dependsOn // []))) as $adj
| ([$root] | closure($adj)) as $reach
| $bom
| .components = [ (.components // [])[] | select( ((.["bom-ref"]) // .purl) as $r | $reach | index($r)) ]
| .dependencies = [ (.dependencies // [])[]
                    | select(.ref as $r | $reach | index($r))
                    | .dependsOn = [ (.dependsOn // [])[] | select(. as $r | $reach | index($r)) ] ]
| .compositions = [ { "aggregate": "complete", "assemblies": [$root], "dependencies": [$root] } ]
