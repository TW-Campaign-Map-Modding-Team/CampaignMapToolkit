rem INFO: This script is used to copy all relevant data and tools for a standalone release

set out_build_dir="CampaignMapToolkit\"

if exist %out_build_dir% rmdir /s /q %out_build_dir%

xcopy /exclude:build_caime_exclude_list.txt CAIME\bin %out_build_dir%\CAIME\
rem xcopy /e Projects %out_build_dir%\Projects\
mkdir %out_build_dir%\Projects\

rem Tools and Templates go inside the CAIME folder: CD packs only that folder into the installer
xcopy /e /exclude:build_caime_exclude_list.txt CAIME\Tools %out_build_dir%\CAIME\Tools\
xcopy /e /exclude:build_caime_templates_exclude_list.txt Templates %out_build_dir%\CAIME\Templates\

rem Copy distribution text files (source of truth lives at repo root)
xcopy /Y CREDITS.txt %out_build_dir%\CAIME\
xcopy /Y EULA.txt %out_build_dir%\CAIME\
xcopy /Y LICENSE %out_build_dir%\CAIME\
xcopy /Y README.md %out_build_dir%\CAIME\

