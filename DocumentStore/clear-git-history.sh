git_dir="/Users/jthijs/Documents/my_projects/taxxor/cms/DocumentStore/data/projects/philips-quarterly-report/ar18"
cache_sub_path="/content/system/cache"


# Switch working directory to the repository
cd ${git_dir}
git status

#
# Remove all history in the repository
#
git checkout --orphan latest_branch

git add -A

git commit -am "<commit><message>Initial commit</message><date epoch=\"1539348769\">12/10/2018 14:52:49</date><author><id>johan.thijs@philips.com</id><name>Johan Thijs</name><ip>::1</ip></author></commit>"     

git branch -D master

git branch -m master

#
# Maintenance of repository
#
# - delete all the objects w/o references
# git prune --progress  
# - aggressively collect garbage; may take a lot of time on large repos               
# git gc --aggressive                  

#
# Remove all exixting tags
#
git tag -d $(git tag -l)

#
# Remove all the files stored in the version cache
#
rm -rf ${git_dir}${cache_sub_path}

cd ..