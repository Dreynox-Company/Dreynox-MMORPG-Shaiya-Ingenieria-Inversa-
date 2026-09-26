# ANI signed pre-roll: real-corpus regression

Qualification run 36120315218 returned 149 EditMode cases: 148 passed, one failed, zero skipped. The new all-52-model preflight failed before the expensive build with `ANI end keyframe precedes start keyframe`. Previously unexercised files are:

- Mob_Orc4_Die.ANI, 32,950 bytes, SHA256 7ba762e356770266a9f2dfe66ab6009d287e93c8bcd0d31df973c6ad22c82bbe. Start raw FFFFFFFF, end 56; 59 bones, 1306 rotation keys, 146 translation keys. Fifty-five keys use raw FFFFFFFF.
- Mob_Zomb_01_Att1.ANI, 18,878 bytes, SHA256 49ea9dfc604fa80ee5c9ba3456ced04358286cbab0995c2dc5dd2382188b2200. Start raw FFFFFFFF, end 51; 36 bones, 705 rotation keys, 127 translation keys. Thirty-one keys use raw FFFFFFFF.

The authored key ordering is signed -1, 1, 3, 5, etc. The reader now recognizes the observed -1 start and translates the entire timeline by +1, preserving relative timing, all keys and the pose values. It records FrameOffset=1; ordinary clips retain their previous time representation. Other inverted ranges, out-of-range keys and unsorted keys remain errors. The duration becomes 57/30 and 52/30 seconds respectively, rather than interpreting -1 as 4,294,967,295.

Four added EditMode cases cover the exact two original clips and synthetic range/key checks. This fixes a demonstrated reader defect; it does not independently certify attack speed or impact timing against the native game.
