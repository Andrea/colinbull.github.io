(*** raw ***)
---
layout: page
title: Blogging with F# on GitHub Pages
---

(**

##Introduction
    
Recently I decided I wanted to move my blog from Wordpress, to a far lighter-weight platform. The platform I chose to host my new blog on was [Github Pages](http://pages.github.com). Basically if you have a github account you get a free pages repository where you can host a blog. You simply create a repository with a name that matches the format `{username}.github.io` and that's basically it. You can now create a `index.htm` page place it into your repository and away you go.

Unfortunately having a completely static site isn't massively useful for a blog. At the very least, you are going to want a templating engine of some sort. Fortunately Github pages, comes armed with [jekyll](https://github.com/jekyll/jekyll) which is a blog-aware static site generator. Jekyll relies quite heavily on having the correct folder structure, this had me chasing my tail for a moment then I found the superb [poole](https://github.com/poole/poole) which generates all of the layout a creates a nice looking minimalist blog. Happy Days!
    
To add a post you simply create a `*.md` or `*.html` and save it to the posts directory, push your changes.

##Leveraging [FSharp.Formatting](http://tpetricek.github.io/FSharp.Formatting)

FSharp Formatting is a library that enables a form of literate programming, where you can embed markdown directly into a `*.fsx` script. Then by running a simple command line tool or a script you can convert the script into a HTML or Latex document. When your chosen out put is html you get tool-tips when you hover over terms in your code, exactly like you would in an IDE. For example, 
*)

let add a b = a + b

printfn "%d" (add 5 15)

(**
Since Jekyll does not directly support `*.fsx` files. We need to extend the structure given to us by poole. The first step I took was to include [Paket](https://github.com/fsprojects/Paket) so I can get hold of nuget packages, that I might require for my scripts. This may seem like an odd requirement at first, but because all of my blog posts will be F# script files which are type checked by FSharp.Formatting I effectively have executable / type safe code samples on my blog :). Once paket was installed I ran

    [lang=batch]
    ./.paket/paket add nuget FSharp.Formatting.CommandTool

This installed the F# command tool, which is a command line wrapper for the FSharp.Formatting library. Next I created a `publish.bat` so I have a single command to update changes to my blog

    [lang=batch]
    @echo off

    call .paket\paket restore
    call tools\fsformatting.exe literate --processDirectory --lineNumbers true --inputDirectory  "code" --outputDirectory "_posts"

    git add --all .
    git commit -a -m %1
    git push

The script above takes a single parameter which is a commit message, this can be run like so.

    [lang=batch]
    ./publish.bat "Added post about F# blogging"

At this point all that is left to-do is write some content in the `code` folder and then run the `publish.bat` once you have created your master piece. Well that was nearly the case for me. It turns out that jekyll requires a header at the top of each page which looks something like the following

    [lang=jekyll]
    ---
    layout: page
    title: your post title
    ---

This presented a little bit of a problem as FSharp.Formatting did not have a way of just emitting raw content. Fortunately for me it does have a concept of commands in the documentation. Basically commands allow you to embed the results of computations or hide certain bits of code you may not want in you documentation (more info on this can be found [here](http://tpetricek.github.io/FSharp.Formatting/evaluation.html)). All I have done is extend this mechanism slightly by adding an extra command `raw`. Which allows you to prefix a block of markup that you do not want the formatter to touch. So at the top of each post I now have something like the following,

    [lang=markdown]
    (*** raw ***)
    ---
    layout: page
    title: your post title
    ----

As of writing this change is not part of the FSharp.Formatting mainline, but there is a [PR](https://github.com/tpetricek/FSharp.Formatting/pull/224). However if you deceide that you like this approach, or just want to play, I have created a github repository [FsBlog](https://github.com/colinbull/FsBlog) that is this exact blog (minus content).

One more thing is if you are developing in Visual Studio then I highly recommend the [Elucidate Extension](https://visualstudiogallery.msdn.microsoft.com/2fc5ccff-f424-4721-ac3f-bb9d4ca7de03), so you can visualize your literate scripts as you work on them.

<img width="800" height="475" src="{{ site.baseurl }}public/blog_imgs/elucidate_screenshot.png" />
*)
