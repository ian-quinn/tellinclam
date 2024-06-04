#pragma once

#include "pch.h"

CGALNATIVE_C_FUNCTION
void OrientedBoudningBoxBySurfaceMesh(
	double* vert_xyz_array, size_t vert_count, /* input - mesh vertices */
	double*& obb_pts_xyz
);

CGALNATIVE_C_FUNCTION
void StraightSkeletonByPolygonWithHoles(
	double* vert_xy_array, int* vert_count_array, size_t hole_count, 
	double*& ss_pts_xy, double*& ss_pts_time, int*& ss_type_mask,
	int*& ss_edge_count
);

CGALNATIVE_C_FUNCTION
void CreateOffsetPolygons(
	double* vert_xy_array, int* vert_count_array, size_t hole_count, double offset, 
	double*& ss_poly_xy, int*& ss_poly_vt, int*& ss_poly_count, int*& ss_pt_count
);

CGALNATIVE_C_FUNCTION
void KruskalMST(
	int* edge_array, double* weight_array, size_t edge_count,
	int*& mst_edge_array, int*& mst_edge_count
);

CGALNATIVE_C_FUNCTION
void ReleaseDoubleArray(double* arr);

CGALNATIVE_C_FUNCTION
void ReleaseIntArray(int* arr);