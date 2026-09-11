
decimal

has? hash-bits [IF]

has? hash-bits Constant hash-bits

Create forth-wordlist here 1 hash-bits lshift cells dup allot erase

\ This is the JAW hash, optimised for the forth dictionary. Rather then
\ taking the first letters we take the last. The string comparison 
\ begins from the beginning and proceeds to the next word as soon as a there
\ are different characters. So, putting all words with the same beginning in
\ one hash bucket is less efficient. Additionally there is a permutation of the
\ last character added. This was done because many forth words end with the letter
\ d and t. The letters d and t differ at bit 4, for hash bits less than 5, this would
\ create an imbalance. Later I found that I accidentally permutated the second last char
\ instead of the last char, however, this resulted in far superior results.
\ Because of memory constraints in ec4th targets, we will work 
\ with just 3 bits, resulting in a 16 byte table in 16 bits systems. Together with
\ the faster traversal, I expect a 10x speedup (TODO: benchmark...).  
\
\ I checked with the body of the current ec4th words (internal words still need to be removed):
\
\     41 0
\     41 1
\     48 2
\     47 3
\     37 4
\     49 5
\     54 6
\     37 7

\ The popular but more complex djb2 hash is defined as:
\
\ : hash-djb2 ( addr len -- u ) &5381 -rot bounds ?do &33 * i c@ + loop ;
\
\ This results in this distribution:
\
\     55 0
\     46 1
\     37 2
\     43 3
\     45 4
\     47 5
\     46 6
\     35 7
\
\ The hash quality is slightly worse for djb2. 17mar26jaw

: dictionary-hash ( addr len -- u )
\ JAW hash. Optimised dictionary hash for forth words as described above.
  1- dup IF + dup 1- c@ dup 2/ 2/ + swap c@ + ELSE + c@ THEN ;

: hash-bucket ( addr len -- n )
  dictionary-hash [ 1 hash-bits lshift 1- ] literal and ;

: find-name-in ( c-addr u wid -- nt | 0 ) 
  \ no name is longer than 31 chars, and the copy at HERE must stay in the pad
  over lcount-mask u> IF drop 2drop 0 EXIT THEN
  >r tolower 2dup hash-bucket cells r> + @ f83search ;

[THEN]
