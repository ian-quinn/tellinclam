# tellinclam
 
![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg)
[![wakatime](https://wakatime.com/badge/user/b04d35f7-79c6-4b67-9dd8-73bd60f22c2f/project/5476af83-7b41-4429-a7f2-2876280f3301.svg)](https://wakatime.com/badge/user/b04d35f7-79c6-4b67-9dd8-73bd60f22c2f/project/5476af83-7b41-4429-a7f2-2876280f3301)

Grasshopper plugin for my own components.
Based on [XingxinHE](https://github.com/XingxinHE)/[CGAL_IN_GRASSHOPPER](https://github.com/XingxinHE/CGAL_IN_GRASSHOPPER)

- CGAL Straight Skeleton wrapper
- CGAL Optimal Bounding Box wrapper
- Boost Kruskal MST wrapper
- Floyd-Warshall algorithm
- Skeleton prone for plumbing network
- Rectilinear Steiner Tree (WIP)
- gbXML deserializer (WIP)

## build event

@Tellinclam Pre-build event
```
XCOPY "$(SolutionDir)deps\$(Configuration)" "$(TargetDir)" /S /Y
```

@Tellinclam Post-build event
```
XCOPY "$(ProjectDir)$(OutputPath)*.dll" "$(USERPROFILE)\AppData\Roaming\Grasshopper\Libraries\$(ProjectName)\" /S /Y
XCOPY "$(ProjectDir)$(OutputPath)*.gha" "$(AppData)\Grasshopper\Libraries\$(ProjectName)\" /Y
```

@CGAL.Wrapper Pre-build event
```
xcopy "$(SolutionDir)deps\$(Configuration)" "$(TargetDir)" /S /Y
```

@CGAL.Native Output Directory
```
$(SolutionDir)deps\$(Configuration)
```