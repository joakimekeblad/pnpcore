# Publishing and promoting pages as news

Once a page has been created it sits in draft status and it will not be seen as news on the site's home page. You can publish a page and promote it by posting the page as news or you can make the page the home page of your site.

> [!Note]
> A page needs to be saved before you can use any of the "promotion" API's.

## Publishing a page

After a page has been created publishing it is as simple as calling the [PublishAsync method](https://pnp.github.io/pnpcore/api/PnP.Core.Model.SharePoint.IPage.html#PnP_Core_Model_SharePoint_IPage_PublishAsync).

```csharp
// Create the page
var page = await context.Web.NewPageAsync();

// Configure the page

// Save the page
await page.SaveAsync("PageA.aspx");

// Publish the page
await page.PublishAsync();
```

## Posting a page as news article

A page can get more visibility by posting it as a news post. Calling the [PromoteAsNewsArticleAsync method](https://pnp.github.io/pnpcore/api/PnP.Core.Model.SharePoint.IPage.html#PnP_Core_Model_SharePoint_IPage_PromoteAsNewsArticleAsync) is all you need to do.

```csharp
// Create the page
var page = await context.Web.NewPageAsync();

// Configure the page

// Save the page
await page.SaveAsync("PageA.aspx");

// Post as news
await page.PromoteAsNewsArticleAsync();

// Publish the page (recommended after posting as news but not required)
await page.PublishAsync();
```

## Demoting a news article back to a regular page

Demoting an existing news post to a regular page can be done with the [DemoteNewsArticleAsync method](https://pnp.github.io/pnpcore/api/PnP.Core.Model.SharePoint.IPage.html#PnP_Core_Model_SharePoint_IPage_DemoteNewsArticleAsync).

```csharp
// demote as news article
await page.DemoteNewsArticleAsync();
```

## Promoting a page as site home page

If you want to set your page as the site's home page you have two options: you can use the convenient [PromoteAsHomePageAsync method](https://pnp.github.io/pnpcore/api/PnP.Core.Model.SharePoint.IPage.html#PnP_Core_Model_SharePoint_IPage_PromoteAsHomePageAsync) on the page object or you can load the web's `RootFolder` and set the `WelcomePage` property to the page you want to set as home page. The first approach is the recommended manner.

```csharp

// Promote as home page of the site

// OPTION 1
await page.PromoteAsHomePageAsync();

// OPTION 2
var web = await context.Web.GetAsync(p => p.RootFolder);
web.RootFolder.WelcomePage = "SitePages/PageA.aspx";
await web.RootFolder.UpdateAsync();
```
