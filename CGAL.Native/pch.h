// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"

// macros
#if defined (_WIN32)
#if defined (CGALNATIVE_DLL_EXPORTS)
#define CGALNATIVE_CPP_CLASS __declspec(dllexport)
#define CGALNATIVE_CPP_FUNCTION __declspec(dllexport)
#define CGALNATIVE_C_FUNCTION extern "C" __declspec(dllexport)
#else
#define CGALNATIVE_CPP_CLASS__declspec(dllimport)
#define CGALNATIVE_CPP_FUNCTION__declspec(dllimport)
#define CGALNATIVE_C_FUNCTION extern "C" __declspec(dllimport)
#endif // CGALNATIVE_DLL_EXPORTS
#endif // _WIN32

// CGAL Library
#include <CGAL/Exact_predicates_inexact_constructions_kernel.h>
#include <CGAL/Surface_mesh.h>
#include <CGAL/optimal_bounding_box.h>
//
#include <CGAL/Polygon_with_holes_2.h>
#include <CGAL/create_straight_skeleton_from_polygon_with_holes_2.h>
//
#include <boost/graph/adjacency_list.hpp>
#include <boost/graph/kruskal_min_spanning_tree.hpp>

typedef CGAL::Exact_predicates_inexact_constructions_kernel    K;
typedef K::Point_3                                             Point_3;
typedef CGAL::Surface_mesh<Point_3>                            Surface_mesh;
typedef CGAL::SM_Vertex_index								   Vertex_index;
//
typedef K::Point_2											   Point_2;
typedef CGAL::Polygon_2<K>									   Polygon_2;
typedef CGAL::Polygon_with_holes_2<K>						   Polygon_with_holes;
typedef CGAL::Straight_skeleton_2<K>						   Ss;
typedef boost::shared_ptr<Ss>								   SsPtr;
//
typedef boost::adjacency_list<boost::vecS, boost::vecS, 
	boost::undirectedS, boost::no_property, 
	boost::property<boost::edge_weight_t, int>>				   Graph;
typedef boost::graph_traits<Graph>::edge_descriptor			   Edge;
typedef std::pair<int, int>									   E;
typedef boost::edge_weight_t								   Wtr;
typedef boost::property_map<Graph,Wtr>						   Pmap;

#endif //PCH_H
