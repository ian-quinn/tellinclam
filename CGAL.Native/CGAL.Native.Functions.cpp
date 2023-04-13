#include "pch.h"
#include "CGAL.Native.Functions.h"

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
	double*& ss_pts_xy, int*& ss_type_mask, 
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

	Halfedge_const_handle	null_halfedge ;
	Vertex_const_handle		null_vertex ;

	// only take the bisectors
	ss_pts_xy = new double[ss.size_of_halfedges() * 4];
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

void ReleaseDoubleArray(double* arr)
{
	delete[] arr;
}

void ReleaseIntArray(int* arr)
{
	delete[] arr;
}