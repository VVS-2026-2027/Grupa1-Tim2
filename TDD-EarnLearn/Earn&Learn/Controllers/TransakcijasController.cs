using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Earn_Learn.Data;
using Earn_Learn.Models;

namespace Earn_Learn.Controllers
{
    public class TransakcijasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransakcijasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Transakcijas
        public async Task<IActionResult> Index()
        {
            return View(await _context.Transakcija.ToListAsync());
        }

        // GET: Transakcijas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transakcija = await _context.Transakcija
                .FirstOrDefaultAsync(m => m.id == id);
            if (transakcija == null)
            {
                return NotFound();
            }

            return View(transakcija);
        }

        // GET: Transakcijas/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Transakcijas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("id,idStudenta,idTutora,iznos,datumUplate,nacinPlacanja,statusPlacanja")] Transakcija transakcija)
        {
            if (ModelState.IsValid)
            {
                _context.Add(transakcija);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(transakcija);
        }

        // GET: Transakcijas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transakcija = await _context.Transakcija.FindAsync(id);
            if (transakcija == null)
            {
                return NotFound();
            }
            return View(transakcija);
        }

        // POST: Transakcijas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("id,idStudenta,idTutora,iznos,datumUplate,nacinPlacanja,statusPlacanja")] Transakcija transakcija)
        {
            if (id != transakcija.id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(transakcija);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransakcijaExists(transakcija.id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(transakcija);
        }

        // GET: Transakcijas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transakcija = await _context.Transakcija
                .FirstOrDefaultAsync(m => m.id == id);
            if (transakcija == null)
            {
                return NotFound();
            }

            return View(transakcija);
        }

        // POST: Transakcijas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transakcija = await _context.Transakcija.FindAsync(id);
            if (transakcija != null)
            {
                _context.Transakcija.Remove(transakcija);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TransakcijaExists(int id)
        {
            return _context.Transakcija.Any(e => e.id == id);
        }
    }
}
