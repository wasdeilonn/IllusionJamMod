# wasdTemplate
Project template for Polyscript. This template is used by the best mod ever, Polibrary, so you know its good.

HUGE SHOUTOUT TO JOUKE/INCOMPLETE_TREE, HE MADE LIKE HALF OF THIS!!

# Tutorial for big stupid
## Setup:
- Rename `wasdTemplate.csproj` and `wasdTemplate.sln`
- Rename the namespace in `Main.cs`
- Edit `manifest.json`
- Rename `wasdTemplate.polymod` on line 21 in `wasdTemplate.csproj`
- Rename stuff on line 5 in `wasdTemplate.sln`

## Usage
- You put sprites and json in `mod`
- You put your .cs files in `src`
- In the root folder, you do `dotnet build [wasdTemplate].csproj` to build
- Building will do 3 things:
  - Add a `[wasdTemplate].dll` in `mod`
  - Zip that into `[wasdTemplate].polymod`, found in the root folder
  - Copy that polymod file into your `Mods` folder
  - What this means is that if you add sprites, edit json, or anything, **you have to build to see the changes**. Other than that, its pretty convenient.

## Usage with Polibrary
- Download `Polibrary.dll`. You can either download it from the github or from [polymod](https://polymod.dev). You need the .dll file
- Put the .dll in `src`
- Add this to line 17 in your .csproj: 
```xml
<ItemGroup>
    <Reference Include="Polibrary">
        <HintPath>.\Polibrary.dll</HintPath>
    </Reference>
</ItemGroup>
```
- Now you can use Polibrary. You can do `using Polibrary.Polyscript` for the Command-Action-Reaction API and VFX.
- Do note that this will not update itself automatically like Polymod. Please make sure your .dll is up-to-date because not having it updated could lead to shit blowing up violently.
