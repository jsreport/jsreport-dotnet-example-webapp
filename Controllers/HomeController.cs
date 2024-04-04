using Microsoft.AspNetCore.Mvc;
using WebApp.Model;
using jsreport.AspNetCore;
using jsreport.Types;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WebApp.Controllers
{
    public class HomeController : Controller
    {
        public IJsReportMVCService JsReportMVCService { get; }

        public HomeController(IJsReportMVCService jsReportMVCService)
        {
            JsReportMVCService = jsReportMVCService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public IActionResult Invoice()
        {
            HttpContext.JsReportFeature().Recipe(Recipe.ChromePdf);

            return View(InvoiceModel.Example());
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public IActionResult InvoiceDownload()
        {
            HttpContext.JsReportFeature().Recipe(Recipe.ChromePdf)
                .OnAfterRender((r) => HttpContext.Response.Headers["Content-Disposition"] = "attachment; filename=\"myReport.pdf\"");

            return View("Invoice", InvoiceModel.Example());
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public async Task<IActionResult> InvoiceWithHeader()
        {
            var header = await JsReportMVCService.RenderViewToStringAsync(HttpContext, RouteData, "Header", new { });

            HttpContext.JsReportFeature()
                .Recipe(Recipe.ChromePdf)
                .Configure((r) => r.Template.Chrome = new Chrome
                {
                    HeaderTemplate = header,
                    DisplayHeaderFooter = true,
                    MarginTop = "1cm",
                    MarginLeft = "1cm",
                    MarginBottom = "1cm",
                    MarginRight = "1cm"
                });

            return View("Invoice", InvoiceModel.Example());
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public IActionResult Items()
        {
            HttpContext.JsReportFeature()
                .Recipe(Recipe.HtmlToXlsx)
                .Configure((r) => r.Template.HtmlToXlsx = new HtmlToXlsx() { HtmlEngine = "chrome" });

            return View(InvoiceModel.Example());
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public IActionResult ItemsExcelOnline()
        {
            HttpContext.JsReportFeature()
                .Configure(req => req.Options.Preview = true)
                .Recipe(Recipe.HtmlToXlsx)
                .Configure((r) => r.Template.HtmlToXlsx = new HtmlToXlsx() { HtmlEngine = "chrome" });

            return View("Items", InvoiceModel.Example());
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public async Task<IActionResult> InvoiceWithCover()
        {
            var coverHtml = await JsReportMVCService.RenderViewToStringAsync(HttpContext, RouteData, "Cover", new { });
            HttpContext.JsReportFeature()
                .Recipe(Recipe.ChromePdf)
                .Configure((r) =>
                {
                    r.Template.PdfOperations = new[]
                    {
                        new PdfOperation()
                        {
                            Template = new Template
                            {
                                Content = coverHtml,
                                Engine = Engine.None,
                                Recipe = Recipe.ChromePdf
                            },
                            Type = PdfOperationType.Prepend
                        }
                    };
                });

            return View("Invoice", InvoiceModel.Example());
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public async Task<IActionResult> ChartWithPrintTrigger()
        {
            HttpContext.JsReportFeature()
                .Recipe(Recipe.ChromePdf)
                .Configure(cfg =>
                {
                    cfg.Template.Chrome = new Chrome
                    {
                        WaitForJS = true
                    };
                });

            return View("Chart", new { });
        }

        [MiddlewareFilter(typeof(JsReportPipeline))]
        public async Task<IActionResult> ToC()
        {
            HttpContext.JsReportFeature()
                .Recipe(Recipe.ChromePdf)
                .Configure(cfg =>
                {
                    cfg.Template.Helpers = @"
const jsreport = require('jsreport-proxy')
const headings = []

function heading(h, opts) {
    const { id, content, parent } = opts.hash
    headings.push({ id, content, parent })

    const headingHtml = `<${h} id='${id}'>${content}</${h}>`

    //we put to the html hidden mark which we in the second render use to find out the page number
    //of the heading
    const hiddenMark = pdfAddPageItem.call(this, {            
            ...opts,
            hash: {
                headingId: id
            }
        })
    
    return headingHtml + hiddenMark
}

async function toc(opts) {
    // we need to postpone the toc printing till all the headings are registered
    // using heading helper
    await jsreport.templatingEngines.waitForAsyncHelpers()
    let res = ''
    for (let { id, content, parent } of headings) {
        res += opts.fn({
            ...this,            
            pageNumber: getPageNumber(id, opts),
            content,
            parent,
            id
        })
    }

    return res
}

function getPageNumber(id, opts) {
    if (!opts.data.root.$pdf) {        
        return 'NA'
    }

    for (let i = 0; i < opts.data.root.$pdf.pages.length; i++) {
        const item = opts.data.root.$pdf.pages[i].items.find(item => item.headingId === id)

        if (item) {
            return i + 1
        }
    }
    return 'NOT FOUND'
}";

                    cfg.Template.Scripts = new[]
                    {
                        new Script
                        {
                            Content = @"
const jsreport = require('jsreport-proxy')

function beforeRender(req, res) {    
    req.options.pdfUtils = { removeHiddenMarks: false }
}

async function afterRender (req, res) {
    if (req.data.secondRender) {
        return
    }

    const p  = await jsreport.pdfUtils.parse(res.content, true)    
    
    const finalR = await jsreport.render({
        template: {
            ...req.template
        },
        data: {
            ...req.data,
            $pdf: p,
            secondRender: true
        }
    })
    res.content = finalR.content
}"
                        }
                    };
                    cfg.Template.Engine = Engine.Handlebars;

                });

            return View("ToC", new { });
        }
    }
}
