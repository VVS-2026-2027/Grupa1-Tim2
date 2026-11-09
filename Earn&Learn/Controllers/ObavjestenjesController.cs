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
    public class ObavjestenjesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ObavjestenjesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Obavjestenjes
        public async Task<IActionResult> Index()
        {
            return View(await _context.Obavjestenje.ToListAsync());
        }

        // GET: Obavjestenjes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var obavjestenje = await _context.Obavjestenje
                .FirstOrDefaultAsync(m => m.id == id);
            if (obavjestenje == null)
            {
                return NotFound();
            }

            return View(obavjestenje);
        }

        // GET: Obavjestenjes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Obavjestenjes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("id,idKorisnika,naslov,sadrzaj,datumSlanja,procitano")] Obavjestenje obavjestenje)
        {
            if (ModelState.IsValid)
            {
                _context.Add(obavjestenje);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(obavjestenje);
        }

        // GET: Obavjestenjes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var obavjestenje = await _context.Obavjestenje.FindAsync(id);
            if (obavjestenje == null)
            {
                return NotFound();
            }
            return View(obavjestenje);
        }

        // POST: Obavjestenjes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("id,idKorisnika,naslov,sadrzaj,datumSlanja,procitano")] Obavjestenje obavjestenje)
        {
            if (id != obavjestenje.id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(obavjestenje);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ObavjestenjeExists(obavjestenje.id))
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
            return View(obavjestenje);
        }

        // GET: Obavjestenjes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var obavjestenje = await _context.Obavjestenje
                .FirstOrDefaultAsync(m => m.id == id);
            if (obavjestenje == null)
            {
                return NotFound();
            }

            return View(obavjestenje);
        }

        // POST: Obavjestenjes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var obavjestenje = await _context.Obavjestenje.FindAsync(id);
            if (obavjestenje != null)
            {
                _context.Obavjestenje.Remove(obavjestenje);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ObavjestenjeExists(int id)
        {
            return _context.Obavjestenje.Any(e => e.id == id);
        }
    }
}
