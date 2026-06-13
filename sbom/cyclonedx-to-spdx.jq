# Derives an artifact-accurate SPDX 2.3 document from the pruned CycloneDX BOM
# (packages/bom.cdx.json). The CycloneDX BOM is the authoritative source; this
# keeps both formats consistent and avoids the GitHub Dependency Graph API,
# whose SBOM describes the whole repository (build/test/release tooling) on the
# default branch rather than the published package.
#
# The root component (metadata.component) and every remaining component become
# SPDX packages; the CycloneDX dependency graph becomes DESCRIBES / DEPENDS_ON
# relationships. Timestamp and namespace are derived from the CycloneDX BOM so
# the result is deterministic. No hardcoded package names are used.

def spdxid($ref): "SPDXRef-Package-" + (($ref | tostring) | gsub("[^a-zA-Z0-9.-]"; "-"));
def license($c): ( [ ($c.licenses // [])[] | (.license.id // .license.name) ] | map(select(. != null))[0] // "NOASSERTION" );
def supplier($c): ( ($c.supplier.name // $c.author) as $n | if $n then ("Organization: " + $n) else "NOASSERTION" end );
def download($c): ( [ ($c.externalReferences // [])[] | select(.type == "distribution") | .url ][0] // "NOASSERTION" );
def pkg($c):
  {
    "SPDXID": spdxid($c["bom-ref"] // $c.purl // $c.name),
    "name": $c.name,
    "versionInfo": ($c.version // "NOASSERTION"),
    "downloadLocation": download($c),
    "filesAnalyzed": false,
    "supplier": supplier($c),
    "licenseConcluded": license($c),
    "licenseDeclared": license($c),
    "copyrightText": ($c.copyright // "NOASSERTION")
  }
  + ( if $c.description then { "description": $c.description } else {} end )
  + ( if $c.purl then { "externalRefs": [ { "referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": $c.purl } ] } else {} end );

. as $bom
| ($bom.metadata.component) as $root
| (($root["bom-ref"]) // $root.purl // $root.name) as $rootref
| ($bom.metadata.timestamp // (now | todateiso8601)) as $ts
| (($bom.serialNumber // "urn:uuid:00000000-0000-0000-0000-000000000000") | sub("^urn:uuid:"; "")) as $serial
| ([$root] + ($bom.components // [])) as $allComponents
| {
    "spdxVersion": "SPDX-2.3",
    "dataLicense": "CC0-1.0",
    "SPDXID": "SPDXRef-DOCUMENT",
    "name": ($root.name + "@" + ($root.version // "NOASSERTION")),
    "documentNamespace": ("https://spdx.org/spdxdocs/" + $root.name + "-" + ($root.version // "0") + "-" + $serial),
    "creationInfo": {
      "created": $ts,
      "creators": ( [ "Tool: dotnet-CycloneDX", "Tool: cyclonedx-to-spdx.jq" ]
                    + ( if ($root.supplier.name // $root.author) then [ "Person: " + ($root.supplier.name // $root.author) ] else [] end ) )
    },
    "packages": [ $allComponents[] | pkg(.) ],
    "relationships": (
        [ { "spdxElementId": "SPDXRef-DOCUMENT", "relationshipType": "DESCRIBES", "relatedSpdxElement": spdxid($rootref) } ]
      + [ ($bom.dependencies // [])[] as $d
          | ($d.dependsOn // [])[] as $dep
          | { "spdxElementId": spdxid($d.ref), "relationshipType": "DEPENDS_ON", "relatedSpdxElement": spdxid($dep) } ]
    )
  }
