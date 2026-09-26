# Original Map1 fighter sword and authored item transforms

## Read-only provenance
The supplied offline reference config `servicios/world/config/character.json` (40921 bytes; SHA256 d4d9e8cef3c29e2f45f2766e6d493bc273d7ea2ebb2adea23a4ac3c9cd08daa2) assigns country0/job0 a starting item type1/typeId1 and ten items type25/typeId1. Its Map1 entry is580,78,1760. This is the supplied offline emulator's loadout, not a recovered proprietary server rule. This change integrates only the sword visual; it does not implement the consumables or an authoritative inventory grant.

The exact original `DATA_Español/binarysdata/dbitemdata.sdata` has28142 rows/70 fields; SHA256 cdb71e93b0f683b6db4adb9c45df7b1d95c481d3ab0d94092018217ad3afaa28. Item1/1 resolves to image0. A separate older root-level table has25294 rows/69 fields; it is not substituted. Resolution uses column names and explicit type/id, never treats an item TypeId as an IT2 ordinal.

Original item/01.itm:85829 bytes, SHA25614e17ddc4bf65deb2b2b1758ae7f3b62ce573546a42b745c0cce48124098f9b3. It contains27 mesh names,36 texture names and80 records. Image0 resolves to01001.3DO and01001.dds. HUMF primary transform:bone21,position(.099,.004,.006),quaternion(-.024678,-.706676,-.024678,.706676). Extended record format1,blend-1,opaque white,rotation0,scale1.

Original item/3do/01001.3do:6340 bytes, SHA25696eebdb03e2df03944b10726e0ade62ee01b4f9b2f84a95ecc926bb0d3dda90d.169 vertices,152 triangles,empty embedded texture name,eight zero trailer bytes. Its explicit IT2 texture item/dds/01001.dds is43856 bytes, SHA2564ea259805f283316defdd1108e2f64a1e6a2972eb7642292fb2dc594f8e209a8.

## Reader and integration
Bounded independent readers support ITM (no transform pairs), IT2 (16 pairs), and the supplied pandaIT2 variant (24 pairs). The latter layout was independently validated to exact EOF:05_01.itm,287650 bytes,181 records, SHA256 c498c7af01ed6e61ffcc4b585f0d04ea015fa7ea231991146d2c477134be44d2. Additional panda slots are preserved numerically, not assigned invented character/class meanings. The17 canonical descriptors plus this variant contain1990 records. The duplicate13.bak.itm is not counted again.

The explicit Map1 builder creates a sword mesh/material/prefab/AttachmentDefinition and binds the authored primary transform to Bone_021 using the existing actor attachment controller. The existing coordinate bridge changes mesh, normals, winding and quaternion basis consistently. A child visual preserves the authored quaternion without converting through Euler angles. Equipment is applied in the Player's Start through the actor, selecting its existing one-handed animation semantics. No original body mesh/skeleton or source DATA is rewritten. Smoothness is a local presentation choice, not a decoded native constant.

Thirty added test cases cover all18 original descriptor files, exact sword source hashes/coordinates, ITM/IT2/panda layouts, unsafe names, invalid indices/floats/quaternions/truncation, 3DO triangle bounds/trailers, item table column reordering/duplicates, and persistent sword attachment following a hand transform. Actual passing status requires Unity CI. The Player qualification additionally requires the equipped original169-vertex sword and authored hand binding before it can report a passed game loop.

Not completed by this change: arbitrary live inventory/equip menus, all weapon material effects, every archetype/panda avatar, server authority or native damage/attack timing. Source format references: matigramirez/Parsec@e5cbc6d72367a4796bad080e4b34edf3c9cc96b8 (Itm/ItmRecord,ItmBoneTransform,3do/_3do,Item/DBItemDataRecord). No upstream code, private server config or original art is copied into the repository.
