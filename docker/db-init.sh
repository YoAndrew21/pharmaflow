#!/bin/sh
set -eu
# Hard stop on any target except the dedicated Compose service/database.
test "$ConnectionStrings__PharmaFlow" = 'Host=postgres;Port=5432;Database=pharmaflow_compose;Username=pharmaflow_app'
./efbundle
dotnet PharmaFlow.Api.dll --seed
