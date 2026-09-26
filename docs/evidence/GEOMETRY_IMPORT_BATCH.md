# Grouped native geometry writes

The source of the currently running Map1 qualification remains immutable. This change targets subsequent imports, not an in-progress Editor instance.

LegacyAssetWriteBatch previously staged writes but called CreateAsset individually for every mesh and numeric ANI clip. DisallowAutoRefresh alone did not group those explicit imports. Only dependency-free Mesh and AnimationClip objects without object-reference curves are now persisted within a short StartAssetEditing/StopAssetEditing scope. Texture imports, materials, terrain, catalogs, subassets and prefab serialization remain outside that suspended scope. Material and package initialization are not deferred. The existing short project root and package-resource preflight remain required.

Every scope resumes imports in finally. Geometry imports finish before material/catalog persistence and before a prefab can load/save their references. Faulted flushes are not retried from Dispose, which would mask the original exception and repeat partial writes. The transaction is not atomic on disk: a failed preparation remains failed and must never be packaged. Original DATA is untouched.

Three Unity regression cases verify mixed mesh/clip/material/catalog prefab references, replacement of an existing mesh, and recovery after a rejected destroyed staged object. Time improvements are not asserted until actual CI measurements exist. The batch log now reports groupedGeometry and faulted beside total writes, flushes and duration.

Reference: Unity 6 AssetDatabase.StartAssetEditing API documents suspension of automatic import and the requirement to resume in finally. Asset loads must not depend on newly suspended imports.
