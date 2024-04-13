oz.ttl is a sample file for the ontology supporting thermal zoning. It will be segregated into three parts in the future: 1. main zoning ontology `oz:` 2. space function ontology specific to building types 3. individuals generated according to the floor plan by OWL API

The sample floor plan:

```
┌─────────21────────┬──────────4──────────┐
|                   |                     |
22        #5        5          #1         10
|                   |                     |
|                   |                     |
├ ─ ─ 16─ ─ ┬───12──┼────1──■─┬ ─ ─ 2 ─ ─ ┤   ──■── Physical partition & door entry
|           31 #10  32        |           |
|           ├───30──┤    #2   3     #3    9   ─ ─ ─ Virtual partition
|           ■       ■         |           |
23    #4    |       6         |           |    #11  Function space index
|           11 #9   └────7────┼─────8─────┤
|           |                 15          |   ──2── Partition index
|           |                 |           |
├ ─ ─ 17─ ─ ┼───13─■┬────14──■┤           |
|           |       |         |     #8    |
|           |       |         ■           |
|     #6    |  #11  |    #7   |           |
24          18      19        20          29
|           |       |         |           |
└─────25────┴───26──┴────27───┴─────28────┘
```