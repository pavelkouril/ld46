// checks the overlap of voxels in one dimension
float omega(float x1, float x2)
{
	return 1 - min(1, abs(x1 - x2));
}

// computes the amount soil to be moved based on velocity
float lambda(int3 abc, int3 ijk)
{
	float3 moved = velocity.Load(int4(ijk, 0));
	return omega(ijk.x + moved.x * _deltaTime, abc.x)
		* omega(ijk.y + moved.y * _deltaTime, abc.y)
		* omega(ijk.z + moved.z * _deltaTime, abc.z);
}

// displace the soil based on velocity
float inflow(int3 ijk, int3 abc)
{
	// density of abc * fraction of abc's displaced volume that overlaps with ijk
	return density.Load(int4(abc, 0)) * lambda(ijk, abc);
}

// return the overflown soil, ijk != abc
float backflow(int3 ijk, int3 abc)
{
	// cant receive backflow from itself
	if (ijk.x == abc.x && ijk.y == abc.y && ijk.z == abc.z)
	{
		return 0;
	}
	float voxelDens = density.Load(int4(abc, 0)) + collision.Load(int4(abc, 0));
	return voxelDens >= 1 ? inflow(abc, ijk) : 0;
}

// -------------------------------------------------------------------------------------------------------

// computes the amount soil to be moved based on force
// ijk current
// abc neighbour
float lambdaForce(int3 ijk, int3 abc)
{
	float3 loadedForce = force.Load(int4(abc, 0));
	float longestComponent = max(abs(loadedForce.x), max(abs(loadedForce.y), abs(loadedForce.z)));
	float3 scaledForce = longestComponent > 0 ? loadedForce / longestComponent : float3(0, 0, 0);
	return omega(abc.x, ijk.x + scaledForce.x) 
		* omega(abc.y, ijk.y + scaledForce.y)
		* omega(abc.z, ijk.z + scaledForce.z);
}

float inflowForce(int3 ijk, int3 abc)
{
	// density of abc * fraction of abc's displaced volume that overlaps with ijk
	return density.Load(int4(abc, 0)) * lambdaForce(ijk, abc);
}

float backflowForce(uint3 ijk, uint3 abc)
{
	// cant receive backflow from itself
	if (ijk.x == abc.x && ijk.y == abc.y && ijk.z == abc.z)
	{
		return 0;
	}
	float voxelDens = density.Load(int4(abc, 0)) + collision.Load(int4(abc, 0));
	return voxelDens >= 1 ? inflowForce(abc, ijk) : 0;
}
