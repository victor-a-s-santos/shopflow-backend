Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, EF Core, PostgreSQL, Clean Architecture, DDD, Catalog, Inventory, seed idempotente e carga inicial de dados para e-commerce.

Objetivo:
Implementar uma carga inicial realista de produtos de moda/roupas no Shopflow, com categorias, atributos, produtos, variações de cor/tamanho, SKUs, estoque inicial e imagens estáticas anexadas ao projeto.

Esta carga será usada para ambientes de desenvolvimento, teste e HML, para que a loja já abra com produtos reais e a plataforma possa ser testada de ponta a ponta.

Importante:

* O Shopflow é uma loja de roupas.
* Não criar produtos de eletrônicos, móveis, garrafas, cosméticos ou categorias fora de moda.
* Usar apenas os produtos e imagens listados neste prompt.
* As imagens foram geradas para uso como imagens demonstrativas de catálogo.
* Não usar marcas, logos ou nomes de marcas reais.
* O seed precisa ser idempotente.
* O seed não pode duplicar produtos, SKUs, categorias ou imagens ao rodar mais de uma vez.
* A carga deve criar estoque inicial para cada SKU.
* A carga deve funcionar em Docker/local/HML.
* Não mexer no frontend nesta etapa.
* Não mexer em Mercado Pago, webhook, checkout, orders, identity ou payments.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Antes de implementar, leia:

* docs/prompts/00-project-context.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/catalog.md, se existir
* docs/inventory.md, se existir
* docs/cart-checkout.md
* módulo Catalog
* módulo Inventory
* seeds existentes do Catalog
* seeds existentes do Inventory
* upload/imagens de produto existentes
* migrations atuais
* docker-compose.yml
* Dockerfile da API
* Program.cs / seeding atual

Seguir o padrão existente do projeto.
Não criar arquitetura paralela.

==================================================
2. IMAGENS A SEREM ANEXADAS AO PROJETO
======================================

O usuário colocará previamente as imagens neste caminho do repo:

apps/api/seed-assets/catalog-products/

Arquivos esperados:

1. saia-feminina-rose.png
2. vestido-feminino-lilas.png
3. shorts-social-feminino-verde.png
4. calca-social-cinza.png
5. calca-jeans-escura-masculina.png
6. jaqueta jeans escura feminina.png
7. blusa-moletom-azul-marinho.png
8. camisa-social-branca.png
9. camiseta-oversize-marrom.png
10. camiseta-basica-verde.png
11. saia-feminina-offwhite.png
12. vestido-feminino-rose.png
13. shorts-social-feminino-offwhite.png
14. calca-social-cinza-escuro.png
15. calca-jeans-masculina.png
16. jaqueta jeans feminina.png
17. blusa-moletom-cinza.png
18. camisa-social-offwhite.png
19. camiseta-oversize-preta.png
20. camiseta-basica-branca.png

Regras:

* Verificar se todos os arquivos existem.
* Se algum arquivo estiver ausente, não falhar a aplicação em produção; logar warning claro.
* Em Development/Test/HML, pode lançar erro se a flag de seed demo estiver habilitada e as imagens estiverem ausentes.
* Não baixar imagens da internet.
* Não gerar imagens.
* Não usar imagens externas.
* Não commitar imagens fora do caminho definido.
* Garantir que o Dockerfile da API copie `apps/api/seed-assets/catalog-products/` para dentro da imagem Docker.
* Garantir que o seed consiga ler as imagens no container.

Estratégia desejada:

* Manter imagens fonte versionadas em:
  apps/api/seed-assets/catalog-products/
* Durante o seed, copiar as imagens para o local público usado pela API para servir imagens de produto.
* Se o projeto já usa `wwwroot/uploads`, copiar para:
  wwwroot/uploads/seed-products/
* Salvar no banco URLs estáveis como:
  /uploads/seed-products/<nome-do-arquivo>.png

Se o projeto já tiver outro padrão para ProductImage/upload, usar o padrão existente e documentar a decisão.

==================================================
3. CONFIGURAÇÃO / FLAGS
=======================

Criar configuração para habilitar/desabilitar essa carga:

Sugestão:

DemoCatalogSeed:
Enabled: true
CopyImages: true
CreateInventory: true
DefaultStockQuantity: 20

Variáveis de ambiente sugeridas:

DemoCatalogSeed__Enabled=true
DemoCatalogSeed__CopyImages=true
DemoCatalogSeed__CreateInventory=true
DemoCatalogSeed__DefaultStockQuantity=20

Regras:

* Em Development pode vir habilitado.
* Em HML pode ser habilitado via env.
* Em Production deve ficar desabilitado por padrão, salvo decisão explícita.
* Não apagar produtos existentes.
* Não recriar produtos se já existirem.
* Não sobrescrever dados manuais do admin, exceto se for seguro e documentado.
* Não duplicar imagens.

==================================================
4. CATEGORIAS
=============

Criar categorias coerentes com loja de roupas.

Se o Catalog já suporta categoria pai/filha, criar hierarquia:

* Feminino

  * Vestidos
  * Saias
  * Shorts
  * Jaquetas
* Masculino

  * Calças
  * Camisas
* Unissex

  * Camisetas
  * Moletons

Se o Catalog atual não suporta hierarquia, criar categorias planas:

* Vestidos
* Saias
* Shorts
* Jaquetas
* Calças
* Camisas
* Camisetas
* Moletons

Regras:

* Usar slug estável.
* Não duplicar se categoria já existir.
* Manter categorias ativas.
* Não criar categorias fora de roupas.

==================================================
5. ATRIBUTOS GLOBAIS
====================

Garantir atributos globais:

1. Cor
   Valores:

   * Branco
   * Off-white
   * Verde suave
   * Verde oliva
   * Preto
   * Marrom
   * Azul jeans
   * Jeans escuro
   * Azul marinho
   * Cinza
   * Cinza escuro
   * Rosé
   * Lilás
   * Terracota

2. Tamanho
   Valores:

   * PP
   * P
   * M
   * G
   * GG
   * 38
   * 40
   * 42
   * 44
   * 46

Regras:

* Reutilizar AttributeDefinition/AttributeValueDefinition existentes, se houver.
* Não duplicar valores.
* Nomes e slugs devem ser estáveis.
* Tamanho numérico deve ser usado em calças masculinas/sociais.
* Tamanho PP/P/M/G/GG deve ser usado nas demais peças.

==================================================
6. PRODUTOS E VARIAÇÕES
=======================

Criar os produtos abaixo.

Todos devem estar ativos.
Todos devem ter descrição curta e descrição longa realistas.
Todos devem ter SKU por combinação Cor + Tamanho.
Todos devem ter preço regular e preço promocional opcional.
Todos devem ter estoque inicial.

---

## Produto 1 — Camiseta Básica Algodão

Categoria:

* Unissex > Camisetas
  ou
* Camisetas, se categoria plana

Slug:

* camiseta-basica-algodao

Descrição curta:
Camiseta básica em algodão, minimalista e confortável para o dia a dia.

Descrição longa:
Camiseta básica de modelagem regular, gola redonda e acabamento limpo. Uma peça versátil para compor looks casuais, usar por baixo de jaquetas ou combinar com jeans, shorts e alfaiataria.

Cores/imagens:

* Branco — camiseta-basica-branca.png
* Verde suave — camiseta-basica-verde.png

Tamanhos:

* PP, P, M, G, GG

Preço:

* Regular: 69.90
* Promocional: 59.90

SKU base:

* CAM-BAS

Exemplos:

* CAM-BAS-BRANCO-PP
* CAM-BAS-VERDE-SUAVE-M

---

## Produto 2 — Camiseta Oversized

Categoria:

* Unissex > Camisetas
  ou
* Camisetas

Slug:

* camiseta-oversized

Descrição curta:
Camiseta oversized de caimento amplo e visual moderno.

Descrição longa:
Camiseta oversized com modelagem ampla, ombros levemente deslocados e tecido confortável. Ideal para composições urbanas, casuais e minimalistas.

Cores/imagens:

* Preto — camiseta-oversize-preta.png
* Marrom — camiseta-oversize-marrom.png

Tamanhos:

* P, M, G, GG

Preço:

* Regular: 89.90
* Promocional: 79.90

SKU base:

* CAM-OVR

---

## Produto 3 — Camisa Social Manga Longa

Categoria:

* Masculino > Camisas
  ou
* Camisas

Slug:

* camisa-social-manga-longa

Descrição curta:
Camisa social de manga longa com visual limpo e elegante.

Descrição longa:
Camisa social de manga longa com colarinho estruturado, botões frontais e acabamento clássico. Uma peça essencial para composições formais, profissionais ou casuais refinadas.

Cores/imagens:

* Branco — camisa-social-branca.png
* Off-white — camisa-social-offwhite.png

Tamanhos:

* P, M, G, GG

Preço:

* Regular: 149.90
* Promocional: 129.90

SKU base:

* CAM-SOC

---

## Produto 4 — Blusa de Moletom com Capuz

Categoria:

* Unissex > Moletons
  ou
* Moletons

Slug:

* blusa-moletom-com-capuz

Descrição curta:
Moletom com capuz, bolso canguru e acabamento confortável.

Descrição longa:
Blusa de moletom com capuz, bolso frontal e punhos canelados. Uma peça prática para dias mais frescos, com visual casual e confortável.

Cores/imagens:

* Cinza — blusa-moletom-cinza.png
* Azul marinho — blusa-moletom-azul-marinho.png

Tamanhos:

* P, M, G, GG

Preço:

* Regular: 179.90
* Promocional: 159.90

SKU base:

* MOL-CAP

---

## Produto 5 — Jaqueta Jeans Feminina

Categoria:

* Feminino > Jaquetas
  ou
* Jaquetas

Slug:

* jaqueta-jeans-feminina

Descrição curta:
Jaqueta jeans feminina em modelagem clássica.

Descrição longa:
Jaqueta jeans feminina com fechamento por botões, bolsos frontais e acabamento estruturado. Uma peça versátil para sobreposição em looks casuais.

Cores/imagens:

* Azul jeans — jaqueta jeans feminina.png
* Jeans escuro — jaqueta jeans escura feminina.png

Tamanhos:

* PP, P, M, G, GG

Preço:

* Regular: 229.90
* Promocional: 199.90

SKU base:

* JAQ-JNS-FEM

Observação:
O nome dos arquivos possui espaços. Tratar corretamente no seed, copiando e gerando URL segura. Se necessário, normalizar o nome ao copiar para `jaqueta-jeans-feminina.png` e `jaqueta-jeans-escura-feminina.png`, mas manter mapeamento documentado.

---

## Produto 6 — Calça Jeans Masculina Reta

Categoria:

* Masculino > Calças
  ou
* Calças

Slug:

* calca-jeans-masculina-reta

Descrição curta:
Calça jeans masculina de corte reto e visual clássico.

Descrição longa:
Calça jeans masculina com modelagem reta, cinco bolsos e acabamento tradicional. Ideal para uso diário, combinando com camisetas, camisas e jaquetas.

Cores/imagens:

* Azul jeans — calca-jeans-masculina.png
* Jeans escuro — calca-jeans-escura-masculina.png

Tamanhos:

* 38, 40, 42, 44, 46

Preço:

* Regular: 189.90
* Promocional: 169.90

SKU base:

* CAL-JNS-MAS

---

## Produto 7 — Calça Social Alfaiataria

Categoria:

* Masculino > Calças
  ou
* Calças

Slug:

* calca-social-alfaiataria

Descrição curta:
Calça social de alfaiataria com corte reto e acabamento elegante.

Descrição longa:
Calça social em tecido de alfaiataria, com corte reto, passantes e pregas discretas. Ideal para composições formais ou looks casuais refinados.

Cores/imagens:

* Cinza — calca-social-cinza.png
* Cinza escuro — calca-social-cinza-escuro.png

Tamanhos:

* 38, 40, 42, 44, 46

Preço:

* Regular: 199.90
* Promocional: 179.90

SKU base:

* CAL-SOC

---

## Produto 8 — Shorts Social Feminino de Linho

Categoria:

* Feminino > Shorts
  ou
* Shorts

Slug:

* shorts-social-feminino-linho

Descrição curta:
Shorts feminino de linho com visual leve e elegante.

Descrição longa:
Shorts social feminino em textura de linho, com pregas frontais, passantes e caimento confortável. Ideal para looks frescos, casuais e sofisticados.

Cores/imagens:

* Off-white — shorts-social-feminino-offwhite.png
* Verde oliva — shorts-social-feminino-verde.png

Tamanhos:

* PP, P, M, G, GG

Preço:

* Regular: 119.90
* Promocional: 99.90

SKU base:

* SHO-LIN-FEM

---

## Produto 9 — Vestido Midi Manga Bufante

Categoria:

* Feminino > Vestidos
  ou
* Vestidos

Slug:

* vestido-midi-manga-bufante

Descrição curta:
Vestido midi feminino com manga bufante e cintura marcada.

Descrição longa:
Vestido midi com decote transpassado, mangas bufantes e cintura marcada. Uma peça feminina, leve e elegante para ocasiões casuais ou especiais.

Cores/imagens:

* Rosé — vestido-feminino-rose.png
* Lilás — vestido-feminino-lilas.png

Tamanhos:

* PP, P, M, G, GG

Preço:

* Regular: 219.90
* Promocional: 189.90

SKU base:

* VES-MID-BUF

---

## Produto 10 — Saia Midi Evasê

Categoria:

* Feminino > Saias
  ou
* Saias

Slug:

* saia-midi-evase

Descrição curta:
Saia midi evasê com caimento fluido e elegante.

Descrição longa:
Saia midi em modelagem evasê, cintura marcada e caimento fluido. Uma peça versátil para composições femininas, minimalistas e elegantes.

Cores/imagens:

* Off-white — saia-feminina-offwhite.png
* Rosé/Terracota — saia-feminina-rose.png

Tamanhos:

* PP, P, M, G, GG

Preço:

* Regular: 159.90
* Promocional: 139.90

SKU base:

* SAI-MID-EVA

==================================================
7. REGRAS DE SKU / VARIANTES
============================

Para cada produto:

* Criar uma variante/SKU por combinação de cor + tamanho.
* SKU code deve ser estável e único.
* Usar nomes normalizados sem acentos no código.
* Exemplos:

  * CAM-BAS-BRANCO-P
  * CAM-BAS-VERDE-SUAVE-M
  * CAM-OVR-PRETO-G
  * VES-MID-BUF-LILAS-PP
  * CAL-JNS-MAS-AZUL-JEANS-42

Regras:

* SKU é a menor unidade vendável.
* Produto não pode ser comprado sem SKU.
* Todo SKU deve estar ativo.
* Todo SKU deve ter preço.
* Todo SKU deve ter atributos Cor e Tamanho vinculados.
* Não criar SKUs duplicados ao rodar seed novamente.

==================================================
8. IMAGENS POR COR
==================

Cada produto deve ter pelo menos uma imagem por cor.

Se o modelo atual suporta imagem por variante/cor:

* vincular a imagem correspondente aos SKUs daquela cor.

Se o modelo atual suporta apenas imagens por produto:

* adicionar as duas imagens na galeria do produto;
* definir a primeira cor listada como imagem principal;
* usar alt text claro incluindo produto e cor.

Alt text sugerido:

* "Camiseta Básica Algodão - Branco"
* "Camiseta Básica Algodão - Verde suave"
* "Vestido Midi Manga Bufante - Lilás"

Ordem:

* Imagem principal sortOrder 0.
* Segunda cor sortOrder 1.

Não duplicar imagens ao rodar seed novamente.

==================================================
9. ESTOQUE INICIAL
==================

Criar estoque inicial para cada SKU.

Quantidade padrão:

* 20 unidades por SKU

Se `DemoCatalogSeed__DefaultStockQuantity` estiver definido, usar esse valor.

Regras:

* Usar o módulo Inventory corretamente.
* Não manipular tabelas diretamente se houver Application Service/Command apropriado.
* Se o seed atual já cria inventory item por sku, seguir padrão.
* Não criar estoque duplicado ao rodar seed novamente.
* Se InventoryItem já existir para SKU, não somar estoque novamente por padrão.
* Registrar movimento inicial se o domínio exigir.
* Descrição do movimento:
  "Carga inicial demo catálogo roupas"

==================================================
10. IDEMPOTÊNCIA
================

O seed deve ser idempotente.

Critérios:

* Rodar 1 vez cria categorias, atributos, produtos, SKUs, imagens e estoque.
* Rodar 2 vezes não duplica nada.
* Rodar 2 vezes não soma estoque novamente.
* Rodar 2 vezes não cria imagens duplicadas.
* Se produto já existe pelo slug, atualizar apenas campos seguros ou pular.
* Se SKU já existe pelo código, pular ou atualizar campos seguros.
* Se imagem já existe pela URL/arquivo, pular.
* Se categoria já existe pelo slug, pular.
* Se atributo/valor já existe pelo nome/slug, pular.

Adicionar logs claros:

* categorias criadas
* produtos criados
* SKUs criados
* imagens copiadas
* estoque criado
* itens já existentes ignorados

==================================================
11. LOCAL DE IMPLEMENTAÇÃO
==========================

Escolha o local seguindo o padrão real do projeto.

Possíveis nomes:

* DemoClothingCatalogSeed
* ClothingCatalogDemoSeed
* CatalogDemoSeed
* DemoProductsSeed

Sugestão:

* Catalog seed cria categorias, atributos, produtos, SKUs e imagens.
* Inventory seed cria estoque para os SKUs criados/existentes.

Se for melhor tecnicamente, criar um orquestrador:

* DemoStorefrontSeed

Mas evitar acoplamento ruim.

Não criar endpoint HTTP para seed.
Seed deve rodar no startup ou por mecanismo de seed existente, condicionado por config/env.

==================================================
12. DOCKER / ARQUIVOS ESTÁTICOS
===============================

Garantir que os arquivos em:

apps/api/seed-assets/catalog-products/

sejam copiados para o container Docker.

Atualizar Dockerfile da API se necessário.

Durante o seed:

* copiar imagens para o webroot público usado pela API;
* preservar extensão `.png`;
* sanitizar nomes com espaços;
* gerar URLs públicas estáveis;
* garantir que as imagens sejam acessíveis pelo frontend.

Exemplo desejado:

* fonte:
  apps/api/seed-assets/catalog-products/jaqueta jeans feminina.png
* destino público:
  wwwroot/uploads/seed-products/jaqueta-jeans-feminina.png
* URL salva:
  /uploads/seed-products/jaqueta-jeans-feminina.png

Se a API já usa outro padrão de upload local, seguir esse padrão.

==================================================
13. TESTES
==========

Criar testes unitários/integrados, se viável, para:

1. Seed cria 10 produtos.
2. Seed cria categorias de roupas.
3. Seed cria atributos Cor e Tamanho.
4. Seed cria SKUs por cor/tamanho.
5. Seed cria imagens por produto/cor.
6. Seed cria estoque inicial para cada SKU.
7. Seed é idempotente.
8. Rodar seed duas vezes não duplica produtos.
9. Rodar seed duas vezes não duplica SKUs.
10. Rodar seed duas vezes não duplica imagens.
11. Rodar seed duas vezes não soma estoque novamente.
12. Product by slug retorna produto com imagens.
13. Product variant endpoint encontra SKU por atributos Cor/Tamanho.
14. Inventory por SKU retorna quantidade disponível.

Quantidade esperada de SKUs:

* Produto 1: 2 cores x 5 tamanhos = 10
* Produto 2: 2 cores x 4 tamanhos = 8
* Produto 3: 2 cores x 4 tamanhos = 8
* Produto 4: 2 cores x 4 tamanhos = 8
* Produto 5: 2 cores x 5 tamanhos = 10
* Produto 6: 2 cores x 5 tamanhos = 10
* Produto 7: 2 cores x 5 tamanhos = 10
* Produto 8: 2 cores x 5 tamanhos = 10
* Produto 9: 2 cores x 5 tamanhos = 10
* Produto 10: 2 cores x 5 tamanhos = 10

Total esperado:

* 10 produtos
* 20 imagens de produto
* 94 SKUs
* 94 inventory items
* 1.880 unidades totais em estoque, se DefaultStockQuantity=20

==================================================
14. DOCUMENTAÇÃO
================

Criar ou atualizar:

* docs/catalog-demo-seed.md
* docs/catalog.md, se existir
* docs/inventory.md, se existir
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/testing.md, se existir

Documentar:

* objetivo da carga demo;
* como habilitar/desabilitar;
* caminho das imagens;
* lista de produtos;
* total esperado de SKUs;
* como rodar local;
* como rodar em Docker;
* como validar imagens no frontend;
* que essa carga é para dev/teste/HML, não produção real.

==================================================
15. BUILD E TESTES
==================

Executar:

dotnet build
dotnet test

Depois, se Docker for usado:

docker compose build api
docker compose up -d api

Validar endpoints públicos:

GET /api/catalog/products
GET /api/catalog/products/by-slug/camiseta-basica-algodao
GET /api/catalog/products/by-slug/vestido-midi-manga-bufante

Validar imagem:

* abrir uma URL gerada em `/uploads/seed-products/...`

Validar inventory:

* buscar SKU criado e chamar GET /api/inventory/skus/{skuId}

==================================================
16. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Onde as imagens foram anexadas no projeto.
3. Como as imagens são copiadas para o diretório público.
4. URLs públicas salvas no banco.
5. Configurações adicionadas.
6. Produtos criados.
7. Categorias criadas.
8. Atributos/valores criados.
9. Quantidade de SKUs esperada/criada.
10. Estoque inicial criado.
11. Como a idempotência foi garantida.
12. Como rodar local.
13. Como rodar em Docker.
14. Testes criados.
15. Resultado de dotnet build/test.
16. Docs atualizadas.
17. Limitações conhecidas.
18. Próximo passo recomendado.

Critérios de aceite:

* 10 produtos de roupas criados.
* 20 imagens anexadas no projeto.
* Imagens copiadas/servidas pela API.
* Produtos aparecem com imagem no catálogo.
* Variações de cor e tamanho existem.
* 94 SKUs criados.
* 94 itens de estoque criados.
* Seed é idempotente.
* Não duplica estoque ao rodar novamente.
* Não cria produtos fora de roupas.
* Não mexe no frontend.
* dotnet build passa.
* dotnet test passa.
* Docs refletem o estado real.
