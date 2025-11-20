<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- base includes -->
	<xsl:include href="_utils.xsl"/>

	<xsl:param name="doc-configuration"/>
	<xsl:param name="pageId">cms_project-details</xsl:param>
	<xsl:param name="projectId"/>
	<xsl:param name="editorId"/>
	<xsl:param name="idFirstEditablePage"/>


	<xsl:variable name="current-depth">
		<xsl:value-of select="count(//item[@id = $pageId]/ancestor::item)"/>
	</xsl:variable>

	<xsl:output method="html" omit-xml-declaration="yes" indent="yes"/>


	<xsl:template match="/">
		<nav id="navbar" class="navbar navbar-inverse">


			<div id="navbar-container" class="navbar-container">
				<div class="navbar-header pull-left">
					<button type="button" class="navbar-toggle" data-toggle="collapse" data-target=".navbar-collapse">
						<span class="icon-bar">
							<xsl:comment/>
						</span>
						<span class="icon-bar">
							<xsl:comment/>
						</span>
						<span class="icon-bar">
							<xsl:comment/>
						</span>
					</button>
					<a class="navbar-brand" href="javascript:navigate('/', true);" target="_top">
						<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 186.42 212.45">
							<title>Taxxor logo</title>
							<g id="Layer_2" data-name="Layer 2">
								<g id="Layer_1-2" data-name="Layer 1">
									<path class="cls-1" d="M72.06,16.05H46.18V86.21H25.4V16.05H0V0H72.06Z"/>
									<path class="cls-1" d="M148.94,182H137.69v30.49H116.91v-86.2h33.93q15.33,0,24,6.8t8.64,19.22q0,9-3.64,14.91t-11.4,9.58l18,34.81v.88H164.16Zm-11.25-16h13.15q5.91,0,8.91-3.12t3-8.69q0-5.58-3-8.76c-2-2.12-5-3.18-8.88-3.18H137.69Z"/>
									<polygon class="cls-1" points="183.98 0 160.37 0 116.69 86.21 140.29 86.21 183.98 0"/>
									<polygon class="cls-1" points="0 126.25 23.61 126.25 67.29 212.45 43.68 212.45 0 126.25"/>
								</g>
							</g>
						</svg>
					</a>
				</div>




				<!-- Collect the nav links, forms, and other content for toggling -->
				<div class="collapse navbar-collapse" id="bs-example-navbar-collapse-1">
					<xsl:choose>
						<xsl:when test="$current-depth = 0"/>
						<xsl:when test="$pageId = 'cms_project-details' or /items/structured/item/sub_items/item[@id = 'cms_project-details']//item[@id = $pageId]">
							<ul class="nav navbar-nav">
								<xsl:apply-templates select="/items/structured/item/sub_items/item[@id = 'cms_project-details']/sub_items/item">
									<xsl:with-param name="level">1</xsl:with-param>
								</xsl:apply-templates>
							</ul>
						</xsl:when>
						<xsl:otherwise> </xsl:otherwise>
					</xsl:choose>



					<div class="navbar-buttons navbar-header navbar-right" role="navigation">
						<ul class="nav navbar-nav">
							<!-- #section:basics/navbar.user_menu -->
							<li class="light-blue">
								<a data-toggle="dropdown" href="#" class="dropdown-toggle" title="Taxxor System Info">
									<i class="ace-icon fa fa-user">
										<xsl:comment/>
									</i>
								</a>
								
								<!--
								<a data-toggle="dropdown" href="#" class="dropdown-toggle" title="User settings">[username] <b class="caret"><xsl:comment/></b></a>
								-->

								<ul class="user-menu dropdown-menu-right dropdown-menu dropdown-yellow dropdown-caret dropdown-close">
									<li>
										<a href="javascript:alert('To be implemented soon...')"> <i class="ace-icon fa fa-user"><xsl:comment/></i> [username]</a>
									</li>

									<li class="divider"/>

									<li>
										<a href="javascript:navigate('/logout');"> <i class="ace-icon fa fa-power-off"><xsl:comment/></i> Logout </a>
									</li>
								</ul>
							</li>
							<li>
								<a href="/custom/docs/taxxor-user-documentation.pdf" target="_blank" title="Taxxor DM documentation">
									<i class="glyphicon glyphicon-question-sign">
										<xsl:comment/>
									</i>
								</a>
							</li>
							<li>
								<a data-toggle="dropdown" href="#" class="dropdown-toggle" title="Taxxor System Info">
									<i class="glyphicon glyphicon-cog">
										<xsl:comment/>
									</i>
								</a>
								<ul class="user-menu dropdown-menu-right dropdown-menu dropdown-yellow dropdown-caret dropdown-close">
									<li>
										<a href="javascript:openAboutModal()">About</a>
									</li>
									<li class="divider"/>
									<li>
										<a href="javascript:openStatusModal()">Status</a>
									</li>
									<xsl:if test="/items/structured/item/sub_items/item[@id = 'cms_administration-page']">
										<li class="divider"/>
										<li>
											<a href="javascript:navigate('{/items/structured/item/sub_items/item[@id='cms_administration-page']/web_page/path}');">
												<xsl:value-of select="/items/structured/item/sub_items/item[@id = 'cms_administration-page']/web_page/linkname"/>
											</a>
										</li>
									</xsl:if>
								</ul>

							</li>
						</ul>
					</div>
				</div>


			</div>
		</nav>
	</xsl:template>


	<xsl:template match="item">
		<xsl:param name="level"/>

		<xsl:variable name="item-id" select="@id"/>

		<xsl:choose>
			<!-- sometimes we do not want to display an item -->
			<xsl:when test="@hidefromui = 'true'"/>
			<xsl:when test="$doc-configuration/configuration/editors/editor[@id = $editorId]/disable/hierarchy/item[@id = $item-id]"/>
			<xsl:otherwise>
				<xsl:variable name="render-submenu">
					<xsl:choose>
						<xsl:when test="number($level) &gt; 0 and $current-depth &gt; 1 and sub_items">yes</xsl:when>
						<xsl:otherwise>no</xsl:otherwise>
					</xsl:choose>
				</xsl:variable>

				<xsl:if test="@data-addseperator">
					<li role="separator" class="divider">
						<xsl:comment/>
					</li>
				</xsl:if>


				<li id="{@id}">
					<xsl:attribute name="class">
						<xsl:choose>
							<xsl:when test="$render-submenu = 'yes' and (@id = $pageId or sub_items//item/@id = $pageId)">dropdown active</xsl:when>
							<xsl:when test="$render-submenu = 'yes'">dropdown</xsl:when>
							<xsl:when test="@id = $pageId">
								<xsl:text>active</xsl:text>
							</xsl:when>
						</xsl:choose>
					</xsl:attribute>

					<xsl:variable name="url">
						<xsl:value-of select="web_page/path"/>
						<!-- append additional querystring variables if needed -->
						<xsl:choose>
							<xsl:when test="@id = 'cms_content-editor'">
								<xsl:text>&amp;edit=true</xsl:text>
								<xsl:call-template name="render-querystring-taxxor-pages">
									<xsl:with-param name="doc-configuration" select="$doc-configuration"/>
									<xsl:with-param name="editorId" select="$editorId"/>
									<xsl:with-param name="projectId" select="$projectId"/>
									<xsl:with-param name="sectionId" select="$idFirstEditablePage"/>
								</xsl:call-template>
							</xsl:when>
							<xsl:when test="@id = 'cms_preview-pdfdocument'">
								<xsl:call-template name="render-querystring-taxxor-pages">
									<xsl:with-param name="doc-configuration" select="$doc-configuration"/>
									<xsl:with-param name="editorId" select="$editorId"/>
									<xsl:with-param name="projectId" select="$projectId"/>
									<xsl:with-param name="sectionId">all</xsl:with-param>
								</xsl:call-template>
							</xsl:when>
						</xsl:choose>
					</xsl:variable>



					<a href="javascript:navigate('{$url}');">
						<xsl:choose>
							<xsl:when test="$render-submenu = 'yes'">
								<xsl:attribute name="class">dropdown-toggle</xsl:attribute>
								<xsl:attribute name="data-toggle">dropdown</xsl:attribute>
								<xsl:attribute name="role">button</xsl:attribute>
								<xsl:attribute name="aria-haspopup">true</xsl:attribute>
								<xsl:attribute name="aria-expanded">false</xsl:attribute>
								<xsl:if test="count(sub_items/item) = 1">
									<xsl:attribute name="onclick">
										<xsl:text>navigate('</xsl:text>
										<xsl:value-of select="sub_items/item/web_page/path"/>
										<xsl:text>');</xsl:text>
									</xsl:attribute>
								</xsl:if>

								<xsl:attribute name="href">#</xsl:attribute>
							</xsl:when>
						</xsl:choose>

						<xsl:value-of select="web_page/linkname"/>
						<xsl:if test="$render-submenu = 'yes'">
							<xsl:text> </xsl:text>
							<span class="caret">
								<xsl:comment/>
							</span>
						</xsl:if>
					</a>

					<!--					
					<xsl:comment>
						- level: <xsl:value-of select="$level"/>
						- current-depth: <xsl:value-of select="$current-depth"/>
					</xsl:comment>
					-->

					<xsl:if test="$render-submenu = 'yes'">
						<xsl:apply-templates select="sub_items">
							<xsl:with-param name="level" select="$level"/>
						</xsl:apply-templates>
					</xsl:if>
				</li>
			</xsl:otherwise>
		</xsl:choose>



		<xsl:if test="number($level) = 0">
			<xsl:apply-templates select="sub_items/item">
				<xsl:with-param name="level" select="number($level) + 1"/>
			</xsl:apply-templates>
		</xsl:if>

	</xsl:template>

	<xsl:template match="sub_items">
		<xsl:param name="level"/>

		<ul class="dropdown-menu">
			<xsl:apply-templates select="item[not(@hidefromui = 'true')]">
				<xsl:with-param name="level" select="number($level) + 1"/>
			</xsl:apply-templates>
		</ul>

	</xsl:template>


</xsl:stylesheet>
