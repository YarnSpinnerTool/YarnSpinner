#!/bin/bash

# References
# https://gist.github.com/vidavidorra/548ffbcdae99d752da02
# https://github.com/miloyip/rapidjson/blob/master/travis-doxygen.sh
# https://docs.travis-ci.com/user/environment-variables/#Encrypted-Variables

# Create a clean gh-pages branch from the master branch
#   cd /path/to/repository
#   git checkout --orphan gh-pages
#   git rm -rf .
#   echo "My gh-pages branch" > README.md
#   git add .
#   git commit -a -m "Clean gh-pages branch"
#   git push origin gh-pages

# On GitHub:
# https://github.com/settings/tokens
# Select "Generate new token"
# Enter "Token description" (eg. "<your_repo> Travis CI Documentation")
# Select "Scope->public_repo"
# Generate token
# Copy the generated token.

# If common to all branches, install generated token as repo variable
# https://docs.travis-ci.com/user/environment-variables/#Defining-Variables-in-Repository-Settings
# Go to Repository in Travis CI
# Settings;
# In Environment Variables section give the variable the Name GH_REPO_TOKEN
# Paste the copied token as Value;
# Click Add;
# Make sure the Display value in build log switch is OFF.

# If different on various branches, encrypt it and add it to .travis.yml
# Install Travis Client:
# https://github.com/settings/tokens
# Run the following command
#   travis encrypt GH_REPO_TOKEN=<copied_personal_acces_github_token>;
# This will give a very long line
#   secure: <encrypted_token>
# Copy this line and add it to the environment variables in the .travis.yml file

# Before this script is used there should already be a gh-pages branch in the
# repository.
#
################################################################################

init () {
    # Title         : DeployDocumentation.sh
    # Date created  : 2017/04/01
    # Notes         :
    AUTHOR="Peter Lawler"
    # BASED ON
    # Title         : generateDocumentationAndDeploy.sh
    # Date created  : 2016/02/22
    # Notes         :
    #__AUTHOR__="Jeroen de Bruijn"
    #
    # Preconditions:
    #   source code directory with a $(TRAVIS_BUILD_DIR) prefix.
    # - A gh-pages branch should already exist. See below for mor info on hoe to
    #   create a gh-pages branch.

    echo "DOXYFILES_ROOT :      $DOXYFILES_ROOT"
    echo "WANTED_DOCS :         $WANTED_DOCS"
    echo "GH_REPO_NAME :        $GH_REPO_NAME"
    echo "GH_REPO_USER :        $GH_REPO_USER"
    GH_REPO_FULL_REF="github.com/$GH_REPO_USER/$GH_REPO_NAME.git"
    echo "GH_REPO_FULL_REF :    $GH_REPO_FULL_REF"
    echo "TRAVIS_BUILD_NUMBER : $TRAVIS_BUILD_NUMBER"
    echo "TRAVIS_COMMIT :       $TRAVIS_COMMIT"
    echo "VERBOSE :             $VERBOSE"

    if [ "$VERBOSE" != "true" ]; then
        VERBOSE=""
    else
        VERBOSE_SWITCH="--verbose"
    fi
}

prepare_doxygen () {
    cd ~
    echo "Setting up the script"
    # Exit with nonzero exit code if anything fails
    set -e

    # Create a clean working directory for this script.
    mkdir $VERBOSE_SWITCH code_docs
    cd code_docs

    # Get the current gh-pages branch
    git clone --branch gh-pages "https://git@$GH_REPO_FULL_REF"
    cd "$GH_REPO_NAME"

    ##### Configure git.
    # Set the push default to simple i.e. push only the current branch.
    git config --global push.default simple
    # Pretend to be an user called Travis CI.
    git config user.name "Travis CI"
    git config user.email "travis@travis-ci.org"

    # Remove everything currently in the gh-pages branch.
    rm --force --recursive $VERBOSE_SWITCH ./*

    # Need to create a .nojekyll file to allow filenames starting with an underscore
    # to be seen on the gh-pages site. Therefore creating an empty .nojekyll file.
    echo "" > .nojekyll

    for l in $WANTED_DOCS; do
        echo "Copying $l"
        cp --recursive $VERBOSE_SWITCH "$DOXYFILES_ROOT/$l" .
    done
}

commit_docs () {
    if  [ -d "html" ] && [ -f "html/index.html" ]; then
        echo 'Uploading documentation to the gh-pages branch...'
        # Add everything in this directory (the Doxygen code documentation) to the
        # gh-pages branch.
        git add --all
        git commit --message "Deploy code docs to GitHub Pages Travis build: ${TRAVIS_BUILD_NUMBER}" --message "Commit: ${TRAVIS_COMMIT}"

        # Force push to the remote gh-pages branch.
        git push --force "https://${GH_REPO_TOKEN}@${GH_REPO_FULL_REF}" > /dev/null 2>&1

        echo "Documentation available at:"
        echo "https://$GH_REPO_USER.github.io/$GH_REPO_NAME/html"
    else
        echo '' >&2
        echo 'Warning: No documentation (html) files have been found!' >&2
        echo 'Warning: Not going to push the documentation to GitHub!' >&2
        exit 1
    fi
}

if [ "${TRAVIS_BRANCH}" != "master" ]; then
    echo "Only commit documentation for the master branch"
    exit
fi
init
prepare_doxygen
commit_docs
