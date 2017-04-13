#!/bin/sh
#
# rebuild.sh: rebuild hypertext with the previous context.
#
# Usage:
#	% sh rebuild.sh
#
cd /home/travis/build/thesecretlab/YarnSpinner && GTAGSCONF=':suffixes=c,h,y,s,S,java,c++,cc,cpp,cxx,hxx,hpp,C,H,php,php3,phtml:skip=GPATH,GTAGS,GRTAGS,GSYMS,HTML/,HTML.pub/,html/,tags,TAGS,ID,y.tab.c,y.tab.h,.notfunction,cscope.out,cscope.po.out,cscope.in.out,.gdbinit,SCCS/,RCS/,CVS/,CVSROOT/,{arch}/,.svn/,.git/,.cvsrc,.cvsignore,.gitignore,.cvspass,.cvswrappers,.deps/,autom4te.cache/,.snprj/:GTAGS=/usr/bin/gtags-parser %s:GRTAGS=/usr/bin/gtags-parser -r %s:GSYMS=/usr/bin/gtags-parser -s %s:' htags -g -s -a -n -v -w -t YarnSpinner /home/travis/build/thesecretlab/YarnSpinner/Documentation/html
