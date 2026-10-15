#!/bin/sh

## ONLY FOR BACKEND (for now) ##

# Set the version of the app: major.minor.patch
# Change major if breaking changes
# Change minor if new features
# Change patch if bug fixes/minor changes
appversion="0.1.0"
publish=false

echo "Checking branches..."
if git rev-parse --abbrev-ref HEAD | grep -iq "main" ;then
  echo "Current branch is main, continuing..."
  publish=true
else
  echo "You're not on 'main' please merge or checkout main"
fi

echo -n "Commit all your changes before continuing... "
read

echo "Release version $appversion"

echo -n "Is this the correct version? (y/n) "
read answer

if echo "$answer" | grep -iq "^n" ;then
  echo "Aborting"
  exit 1
fi

echo "Building and pushing the docker images"

# Build and push the docker image for the garage404 : latest
docker build -t larguma/garage404-backend:latest garage404-backend/
if $publish
  docker push larguma/garage404-backend:latest
fi

# Build and push the docker image for the garage404 : version x.x.x
docker build -t larguma/garage404-backend:$appversion garage404-backend/
if $publish
  docker push larguma/garage404-backend:$appversion
fi

echo "Done"

if !$publish
  echo "Only tags on main"
  exit 1
fi

echo "Tagging the release in git"

# Tag the release in git
git tag -a v$appversion -m "Release version $appversion"

echo -n "Would you like to push now? (y/n) "
read answer

if echo "$answer" | grep -iq "^n" ;then
  echo "Aborting"
  exit 1
else
  echo "Pushing"
  git push origin v$appversion
  echo "Done"
  exit 1
fi
