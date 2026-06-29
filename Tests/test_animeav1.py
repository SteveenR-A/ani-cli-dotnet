import urllib.request
from bs4 import BeautifulSoup
import json

req = urllib.request.Request('https://animeav1.com/', headers={'User-Agent': 'Mozilla/5.0'})
html = urllib.request.urlopen(req).read().decode('utf-8')
soup = BeautifulSoup(html, 'html.parser')

print("--- EPISODIOS RECIENTES ---")
episodes_section = soup.find(lambda tag: tag.name == 'h2' and 'Episodios' in tag.text)
if episodes_section:
    container = episodes_section.find_parent('section')
    articles = container.find_all('article')
    for art in articles[:3]:
        title = art.find('div', class_='text-2xs').text if art.find('div', class_='text-2xs') else 'No title'
        ep_num = art.find('span', class_='text-lead font-bold').text if art.find('span', class_='text-lead font-bold') else 'No ep'
        link = art.find('a', href=True)['href'] if art.find('a', href=True) else 'No link'
        print(f"[{ep_num}] {title} -> {link}")

print("\n--- ANIMES RECIENTES ---")
animes_section = soup.find(lambda tag: tag.name == 'h2' and 'Animes' in tag.text)
if animes_section:
    container = animes_section.find_parent('section')
    articles = container.find_all('article')
    for art in articles[:3]:
        title = art.find('h3').text if art.find('h3') else 'No title'
        link = art.find('a', href=True)['href'] if art.find('a', href=True) else 'No link'
        print(f"{title} -> {link}")
