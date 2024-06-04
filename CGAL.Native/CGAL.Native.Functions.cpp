#include "pch.h"
#include "CGAL.Native.Functions.h"


void KruskalMST(
	int* edge_array, double* weight_array, size_t edge_count,
	int*& mst_edge_array, int*& mst_edge_count
)
{
	E edges[100];
	double weights[100];
	std::size_t num_edges = 0;
	for (size_t i = 0; i < edge_count; i++)
	{
		edges[i] = E(edge_array[2 * i], edge_array[2 * i + 1]);
		weights[i] = weight_array[i];
		num_edges++;
	}
	
	/*E edge_array[] = { E(0, 1), E(0, 2), E(1, 2), E(2, 3), E(3, 1) };
	int weights[] = { 1, 2, 1, 1, 2 };*/

	//std::size_t num_edges = sizeof(edges) / sizeof(E);

#if defined(BOOST_MSVC) && BOOST_MSVC <= 1300
	Graph g(num_nodes);
	property_map<Graph, edge_weight_t>::type weightmap = get(edge_weight, g);
	for (std::size_t j = 0; j < num_edges; ++j) {
		Edge e; bool inserted;
		boost::tie(e, inserted) = add_edge(edges[j].first, edges[j].second, g);
		weightmap[e] = weights[j];
	}
#else
	Graph g(edges, edges + num_edges, weights, static_cast<int>(num_edges));
#endif
	Pmap::type weight = get(Wtr::edge_weight, g);
	std::vector<Edge> spanning_tree;

	kruskal_minimum_spanning_tree(g, std::back_inserter(spanning_tree));

	// the edge amount must be less than the original graph, or equal
	// it is a safe choice to declare an array like this
	mst_edge_array = new int[static_cast<int>(num_edges) * 2];

	int edgeCounter = 0;
	for (std::vector < Edge >::iterator ei = spanning_tree.begin();
		ei != spanning_tree.end(); ++ei)
	{
		mst_edge_array[2 * edgeCounter] = source(*ei, g);
		mst_edge_array[2 * edgeCounter + 1] = target(*ei, g);
		edgeCounter++;
	}

	mst_edge_count = new int[1];
	mst_edge_count[0] = edgeCounter;

	return;
}

void OrientedBoudningBoxBySurfaceMesh(
	double* vert_xyz_array, size_t vert_count, /* input - mesh vertices */
	double*& obb_pts_xyz
)
{
	// declare the surface mesh
	std::vector<Point_3> points;

	// fill the info of vertices
	for (size_t i = 0; i < vert_count; i++)
	{
		points.push_back(
			Point_3(vert_xyz_array[3 * i + 0],
				vert_xyz_array[3 * i + 1],
				vert_xyz_array[3 * i + 2])
		);
	}

	// declare output
	std::array<Point_3, 8> obb_points;
	CGAL::oriented_bounding_box(points, obb_points);

	obb_pts_xyz = new double[obb_points.size() * 3];

	int i = 0, count = 0;
	for (Point_3 pt : obb_points)
	{
		obb_pts_xyz[i++] = double(pt.x());
		obb_pts_xyz[i++] = double(pt.y());
		obb_pts_xyz[i++] = double(pt.z());
	}

	return;
}

void StraightSkeletonByPolygonWithHoles(
	double* vert_xy_array, int* vert_count_array, size_t hole_count, 
	double*& ss_pts_xy, double*& ss_pts_time, int*& ss_type_mask, 
	int*& ss_edge_count
)
{
	// declare the outer polygon
	Polygon_2 outer;
	int pt_count = 0;
	for (int i = 0; i < vert_count_array[0]; i++)
	{
		outer.push_back(
			Point_2(
				vert_xy_array[2 * pt_count + 0],
				vert_xy_array[2 * pt_count + 1])
		);
		pt_count += 1;
	}
	//assert(outer.is_counterclockwise_oriented());

	// declare the inner hole
	// pending
	Polygon_with_holes poly(outer);

	for (size_t i = 1; i < hole_count; i++)
	{
		Polygon_2 hole;
		for (int j = 0; j < vert_count_array[i]; j++)
		{
			hole.push_back(
				Point_2(
					vert_xy_array[2 * pt_count + 0],
					vert_xy_array[2 * pt_count + 1])
			);
			pt_count += 1;
		}
		//assert(hole.is_clockwise_oriented());
		poly.add_hole(hole);
	}

	// declare output
	SsPtr iss = CGAL::create_interior_straight_skeleton_2(poly) ;

	// how to decode iss into list of vertice pairs?
	Ss ss = *iss ;

	typedef typename Ss::Vertex_const_handle		Vertex_const_handle ;
	typedef typename Ss::Halfedge_const_handle		Halfedge_const_handle ;
	typedef typename Ss::Halfedge_const_iterator	Halfedge_const_iterator ;
	typedef typename Ss::Vertex_const_iterator		Vertex_const_iterator ;

	Halfedge_const_handle	null_halfedge ;
	Vertex_const_handle		null_vertex ;

	// only take the bisectors
	ss_pts_xy = new double[ss.size_of_halfedges() * 4];
	ss_pts_time = new double[ss.size_of_halfedges() * 2];
	ss_type_mask = new int[ss.size_of_halfedges()];
	ss_edge_count = new int[1];
	ss_edge_count[0] = ss.size_of_halfedges();
	int count = 0;


	for (Halfedge_const_iterator i = ss.halfedges_begin(); i != ss.halfedges_end(); ++i)
	{
		ss_pts_xy[count * 4 + 0] = i->opposite()->vertex()->point().x();
		ss_pts_xy[count * 4 + 1] = i->opposite()->vertex()->point().y();
		ss_pts_xy[count * 4 + 2] = i->vertex()->point().x();
		ss_pts_xy[count * 4 + 3] = i->vertex()->point().y();
		ss_pts_time[count * 2 + 0] = CGAL::to_double(i->opposite()->vertex()->time());
		ss_pts_time[count * 2 + 1] = CGAL::to_double(i->vertex()->time());
		
		if (i->is_bisector())
		{
			if (i->is_inner_bisector())
			{
				ss_type_mask[count] = 0;
			}
			else
			{
				ss_type_mask[count] = 1;
			}
		}
		else
		{
			ss_type_mask[count] = 2;
		}
		count++;
	}

	return;
}

void CreateOffsetPolygons(
	double* vert_xy_array, int* vert_count_array, size_t hole_count, double offset, 
	double*& ss_poly_xy, int*& ss_poly_vt, int*& ss_poly_count, int*& ss_pt_count
)
{
	// declare the outer polygon
	Polygon_2 outer;
	int pt_count = 0;
	for (int i = 0; i < vert_count_array[0]; i++)
	{
		outer.push_back(
			Point_2(
				vert_xy_array[2 * pt_count + 0],
				vert_xy_array[2 * pt_count + 1])
		);
		pt_count += 1;
	}
	//assert(outer.is_counterclockwise_oriented());

	// declare the inner hole
	// pending
	Polygon_with_holes poly(outer);

	for (size_t i = 1; i < hole_count; i++)
	{
		Polygon_2 hole;
		for (int j = 0; j < vert_count_array[i]; j++)
		{
			hole.push_back(
				Point_2(
					vert_xy_array[2 * pt_count + 0],
					vert_xy_array[2 * pt_count + 1])
			);
			pt_count += 1;
		}
		//assert(hole.is_clockwise_oriented());
		poly.add_hole(hole);
	}

	// declare output
	SsPtr iss = CGAL::create_interior_straight_skeleton_2(poly) ;

	// how to decode iss into list of vertice pairs?
	Ss ss = *iss ;

	// set two array
	// one to record the vertices coordinate [x1, y1, x2, y2, x3, y3, ... ]
	// one to record the number of vertices of each polygon
	double lOffset = offset;
	PolygonPtrVector offset_polygons = CGAL::create_offset_polygons_2<Polygon_2>(lOffset, ss);

	// only take the bisectors
	// ss_poly_xy:		x y x y x y .... x y x y x y x y ...
	// ss_poly_vt:			3                   4       ...   
	// ss_poly_count:	            2
	//
	
	int memosize = 0;
	for (typename PolygonVector::const_iterator pi = offset_polygons.begin(); pi != offset_polygons.end(); ++pi)
	{
		Polygon_2 poly = **pi;
		memosize += poly.size();
	}

	ss_poly_xy = new double[memosize * 2];
	ss_poly_vt = new int[offset_polygons.size()];
	ss_poly_count = new int[1];
	ss_pt_count = new int[1];
	ss_poly_count[0] = offset_polygons.size();

	int poly_count = 0;
	int accu_count = 0; // accumulate the number of all points recorded
	for (typename PolygonVector::const_iterator pi = offset_polygons.begin(); pi != offset_polygons.end(); ++pi)
	{
		Polygon_2 poly = **pi;
		int vt_count = 0;
		for (typename Polygon_2::Vertex_const_iterator vi = poly.vertices_begin(); vi != poly.vertices_end(); ++vi)
		{
			Point_2 pt = *vi;
			ss_poly_xy[accu_count * 2 + vt_count * 2 + 0] = pt.x();
			ss_poly_xy[accu_count * 2 + vt_count * 2 + 1] = pt.y();
			vt_count++;
		}
		ss_poly_vt[poly_count] = vt_count;
		accu_count += vt_count;
		poly_count++;
	}
	ss_pt_count[0] = accu_count;

	return;
}

void ReleaseDoubleArray(double* arr)
{
	delete[] arr;
}

void ReleaseIntArray(int* arr)
{
	delete[] arr;
}