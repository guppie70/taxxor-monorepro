<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- base includes -->
	<xsl:include href="_utils.xsl"/>

	<xsl:param name="doc-configuration"/>
	<xsl:param name="output-channel-reference">-</xsl:param>
	<xsl:param name="render-full-page">yes</xsl:param>
	<xsl:param name="filter-full-list">yes</xsl:param>
	<xsl:param name="filter-additional-info">yes</xsl:param>
    <xsl:param name="filter-contentstatus-markers">yes</xsl:param>
	<xsl:param name="mode">default</xsl:param>
	<xsl:param name="user-simulator">no</xsl:param>
	<xsl:param name="hierarchy-type">none</xsl:param>
	<xsl:param name="contentlang"/>
	<xsl:param name="master-outputchannel-id"></xsl:param>
	<xsl:param name="master-outputchannel-name"></xsl:param>
	<xsl:param name="slave-outputchannel-ids"></xsl:param>
	<xsl:param name="slave-outputchannel-names"></xsl:param>
	<xsl:param name="linked-outputchannel-names"></xsl:param>
	<xsl:param name="locked">no</xsl:param>
	<xsl:param name="lock-user-id"></xsl:param>
	<xsl:param name="lock-user-name"></xsl:param>

	<xsl:variable name="output-channel-id">
		<xsl:choose>
			<xsl:when test="$output-channel-reference = '-'">
				<xsl:value-of select="/hierarchies/output_channel/@id"/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$output-channel-reference"/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:variable>

    <xsl:variable name="contentstatus-attribute-name" select="concat('data-contentstatus-', $contentlang)"/>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>


	<xsl:template match="/">

		<xsl:choose>
			<xsl:when test="$render-full-page = 'yes'">
				<div class="row hierarchycontrols">
					<div class="col-sm-6 left">
						<h3 class="row header smaller grey">
							<span class="col-xs-12">Output channel content<span class="lock-information"><xsl:comment>.</xsl:comment></span></span>
						</h3>

						<!-- select box to select the output channel -->

						<form class="form-inline">
							<div class="form-group">
								<label for="select-output-channel" class="output-channel-label">Output channel: </label>
								<select id="select-output-channel" name="select-output-channel" class="form-control input-sm">
									<xsl:for-each select="/hierarchies/output_channel">
										<option>
											<xsl:attribute name="value">
												<xsl:value-of select="concat('octype=', @type, ':ocvariantid=', @id, ':oclang=', @lang)"/>
											</xsl:attribute>
											<xsl:if test="@id = $output-channel-id">
												<xsl:attribute name="selected">
													<xsl:text>selected</xsl:text>
												</xsl:attribute>
											</xsl:if>
											<xsl:value-of select="name"/>
										</option>
									</xsl:for-each>
								</select>


								<!--
							
							<div class="form-group">
								<label for="select-output-channel" class="col-sm-3 output-channel-label">Output channel: </label>
								<div class="col-sm-4">
									<select id="select-output-channel" name="select-output-channel" class="form-control input-sm">
										<xsl:for-each select="/hierarchies/output_channel">
											<option>
												<xsl:attribute name="value">
													<xsl:value-of select="concat('octype=', @type, ':ocvariantid=', @id, ':oclang=', @lang)"/>
												</xsl:attribute>
												<xsl:if test="@id = $output-channel-id">
													<xsl:attribute name="selected">
														<xsl:text>selected</xsl:text>
													</xsl:attribute>
												</xsl:if>
												<xsl:value-of select="name"/>
											</option>
										</xsl:for-each>
									</select>
									


								</div>-->

								<!-- Buttons for future functionality -->
								<!--
								<button class="btn btn-primary btn-xs" id="btn-clone-outputchannel" disabled="disabled">
									<span class="glyphicon glyphicon-duplicate">
										<xsl:comment>.</xsl:comment>
									</span>
									<xsl:text> Clone</xsl:text>
								</button>
								<button class="btn btn-primary btn-xs" id="btn-clone-outputchannel" disabled="disabled">
									<span class="glyphicon glyphicon-plus">
										<xsl:comment>.</xsl:comment>
									</span>
									<xsl:text> New</xsl:text>
								</button>
								<button class="btn btn-primary btn-xs" id="btn-clone-outputchannel" disabled="disabled">
									<span class="glyphicon glyphicon-trash">
										<xsl:comment>.</xsl:comment>
									</span>
									<xsl:text> Remove</xsl:text>
								</button>								
								-->
							</div>
                            <div>
								<label class="checkbox-inline">
									<input type="checkbox" id="chk-filter-contentstatus-markers" name="chk-filter-contentstatus-markers">
										<xsl:if test="$filter-contentstatus-markers = 'yes'">
											<xsl:attribute name="checked">checked</xsl:attribute>
										</xsl:if>
									</input>
									<xsl:text> hide content status markers</xsl:text>
								</label>
                            </div>
						</form>

					</div>
					<div class="vspace-8-sm"><!--.--></div>

					<div class="col-sm-6 right">
						<h3 class="row header smaller grey">
							<span class="col-xs-6">Content blocks</span>
							<span class="col-xs-6">
								<span class="pull-right inline">
									<button class="btn btn-primary btn-xs" id="btn-save">
										<span class="glyphicon glyphicon-floppy-disk">
											<xsl:comment>.</xsl:comment>
										</span>
										<xsl:text> Save</xsl:text>
									</button>
								</span>
							</span>
						</h3>

						<!-- check box to filter the complete list from all the items that are already available in the hierarchy -->
						<div class="form-horizontal">
							<div>
								<button class="btn btn-primary btn-xs" id="btn-create-new-section">
									<span class="glyphicon glyphicon-plus">
										<xsl:comment>.</xsl:comment>
									</span>
									<xsl:text> New</xsl:text>
								</button>
								<button class="btn btn-primary btn-xs" id="btn-delete-sections" disabled="disabled">
									<span class="glyphicon glyphicon-trash">
										<xsl:comment>.</xsl:comment>
									</span>
									<xsl:text> Remove</xsl:text>
								</button>
							</div>
							<div>
								<label class="checkbox-inline">
									<input type="checkbox" id="chk-filter-linked-items" name="chk-filter-linked-items">
										<xsl:if test="$filter-full-list = 'yes'">
											<xsl:attribute name="checked">checked</xsl:attribute>
										</xsl:if>
									</input>
									<xsl:text> hide blocks in use</xsl:text>
								</label>
								<label class="checkbox-inline">
									<input type="checkbox" id="chk-filter-additional-info" name="chk-filter-additional-info">
										<xsl:if test="$filter-additional-info = 'yes'">
											<xsl:attribute name="checked">checked</xsl:attribute>
										</xsl:if>
									</input>
									<xsl:text> hide extra info</xsl:text>
								</label>
								<!--
								<label class="checkbox-inline">
									<input type="checkbox" id="chk-filter-additional-info" name="chk-filter-additional-info">
										<xsl:if test="$filter-additional-info = 'yes'">
											<xsl:attribute name="checked">checked</xsl:attribute>
										</xsl:if>
									</input>
									<xsl:text> hide extra info</xsl:text>
								</label>
								-->
							</div>
						</div>
					</div>

				</div>

				<div class="loadingiconwrapper"><!-- . --></div>

				<div class="hierarchy-content">
					<!--
					<xsl:call-template name="render-tree"/>
					--> </div>

			</xsl:when>
			<xsl:otherwise>
				<xsl:choose>
					<xsl:when test="$mode = 'render-all-items'">
						<xsl:call-template name="render-all-items"/>
					</xsl:when>
					<xsl:when test="$mode = 'acl-overview'">
						<xsl:call-template name="acl-overview"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:call-template name="render-tree"/>
					</xsl:otherwise>
				</xsl:choose>

			</xsl:otherwise>
		</xsl:choose>


	</xsl:template>


	<xsl:template name="acl-overview">
		<div class="outputchannelhierarchy">
			<ul class="dd-list">
				<xsl:apply-templates select="/items/structured//item[@id = 'cms_project-details']">
					<xsl:with-param name="item-type">
						<xsl:text>hierarchical</xsl:text>
					</xsl:with-param>
				</xsl:apply-templates>
			</ul>
		</div>
	</xsl:template>



	<xsl:template name="render-tree">

		<div class="row hierarchycontents hierarchytype-{$hierarchy-type}" data-hierarchylocked="{$locked}" data-hierarchylock-username="{$lock-user-name}" data-hierarchylock-userid="{$lock-user-id}">
			<div class="col-sm-6">
				<!-- the tree list -->
				<div>
                    <xsl:attribute name="class">
                        <xsl:text>dd hierarchy-wrapper hierarchical-list</xsl:text>
                        
						<xsl:if test="$filter-additional-info = 'yes'">
							<xsl:text> filter-additional-info</xsl:text>
						</xsl:if>
                        <xsl:if test="$filter-contentstatus-markers = 'yes'">
							<xsl:text> filter-contentstatus-markers</xsl:text>
						</xsl:if>
                    </xsl:attribute>

					<!-- master indicator -->
					<xsl:choose>
						<xsl:when test="$hierarchy-type = 'master'">
							<div class="hierarchy-indicator hierarchy-master">
								<span>Master hierarchy for:<small><pre><xsl:value-of select="$slave-outputchannel-names"/></pre></small></span>
							</div>
						</xsl:when>
						<xsl:when test="$hierarchy-type = 'slave'">
							<div class="hierarchy-indicator hierarchy-slave">
								<span>Slave hierarchy of:<small><xsl:value-of select="$master-outputchannel-name"/></small></span>
							</div>
						</xsl:when>
						<xsl:when test="$hierarchy-type = 'linked'">
							<div class="hierarchy-indicator hierarchy-linked">
								<span>Linked with hierarchy of:<small><pre><xsl:value-of select="$linked-outputchannel-names"/></pre></small></span>
							</div>
						</xsl:when>
					</xsl:choose>
					
					
					<xsl:apply-templates select="/hierarchies/output_channel[@id = $output-channel-id]/items/structured"/>
				</div>

			</div>

			<div class="vspace-16-sm"/>

			<div class="col-sm-6">

				<!-- all items list -->
				<div>
					<xsl:attribute name="class">
						<xsl:text>dd hierarchy-wrapper full-list</xsl:text>
						<xsl:if test="$filter-full-list = 'yes'">
							<xsl:text> filter-used</xsl:text>
						</xsl:if>
						<xsl:if test="$filter-additional-info = 'yes'">
							<xsl:text> filter-additional-info</xsl:text>
						</xsl:if>
					</xsl:attribute>
					<xsl:call-template name="render-all-items"/>
				</div>
			</div>

		</div>
	</xsl:template>

	<xsl:template name="render-all-items">
		<xsl:choose>
			<xsl:when test="$mode = 'render-all-items'">
				<ul class="dd-list">
					<xsl:apply-templates select="//item">
						<xsl:with-param name="item-type">
							<xsl:text>unstructured</xsl:text>
						</xsl:with-param>
						<xsl:with-param name="view">
							<xsl:text>sequential</xsl:text>
						</xsl:with-param>
					</xsl:apply-templates>
				</ul>
				<div class="metadata">
					<div class="sectionids">
						<xsl:for-each select="//item">
							<xsl:value-of select="@section-id"/>
							<xsl:if test="position() != last()">
								<xsl:text>,</xsl:text>
							</xsl:if>
						</xsl:for-each>
					</div>
				</div>
			</xsl:when>
			<xsl:otherwise>
				<ul class="dd-list">
					<xsl:apply-templates select="/hierarchies/item_overview/items/item">
						<xsl:with-param name="item-type">
							<xsl:text>unstructured</xsl:text>
						</xsl:with-param>
						<xsl:with-param name="view">
							<xsl:text>sequential</xsl:text>
						</xsl:with-param>
					</xsl:apply-templates>
				</ul>
				<div class="metadata">
					<div class="sectionids">
						<xsl:for-each select="/hierarchies/item_overview/items/item">
							<xsl:value-of select="@section-id"/>
							<xsl:if test="position() != last()">
								<xsl:text>,</xsl:text>
							</xsl:if>
						</xsl:for-each>
					</div>
				</div>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>


	<xsl:template match="structured">
		<ul class="dd-list">
			<xsl:apply-templates select="item">
				<xsl:with-param name="item-type">
					<xsl:text>hierarchical</xsl:text>
				</xsl:with-param>
			</xsl:apply-templates>
		</ul>
	</xsl:template>

	<xsl:template match="item">
		<xsl:param name="item-type"/>
		<xsl:param name="view">hierarchical</xsl:param>
		
		<xsl:variable name="item-id" select="@id"/>
		<xsl:variable name="data-ref" select="@data-ref"/>
		<xsl:variable name="dd-content-class">
			<xsl:text>dd-content</xsl:text>
			
			<xsl:choose>
				<xsl:when test="$mode = 'acl-overview' and $user-simulator = 'yes'">
					<!-- append additional class based on the permissions -->
					<xsl:choose>
						<!-- matches on the Taxxor DM fixed top menu items -->
						<xsl:when test="special_permissions and permissions/permission">
							<xsl:variable name="contains-special-permission">
								<xsl:for-each select="special_permissions/permission">
									<xsl:variable name="current-special-permission" select="./@id"/>
									<xsl:if test="//item[@id = $item-id]/permissions/permission[@id = $current-special-permission]">yes</xsl:if>
								</xsl:for-each>
							</xsl:variable>
							
							<xsl:choose>
								<xsl:when test="contains($contains-special-permission, 'yes')">
									<xsl:text> editpermissions</xsl:text>
								</xsl:when>
								<xsl:otherwise>
									<xsl:text> nopermissions</xsl:text>
								</xsl:otherwise>
							</xsl:choose>
							
							<!--<xsl:text> contains-special-permission: </xsl:text>
									<xsl:value-of select="$contains-special-permission"/>-->
							
						</xsl:when>
						
						<xsl:when test="permissions/permission[@id = 'editcontent'] or permissions/permission[@id = 'all']">
							<xsl:text> editpermissions</xsl:text>
						</xsl:when>
						<xsl:when test="permissions/permission[@id = 'view']">
							<xsl:text> viewpermissions</xsl:text>
						</xsl:when>
						
						<xsl:otherwise>
							<xsl:text> nopermissions</xsl:text>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:when>
			</xsl:choose>

            <!-- content status indicator -->
            <xsl:if test="@*[name() = $contentstatus-attribute-name]">
                <xsl:text> contentstatus-</xsl:text>
                <xsl:value-of select="@data-contentstatus-en"/>
            </xsl:if>
		</xsl:variable>

		<li hierarchy-id="{@id}" data-ref="{@data-ref}">
			<xsl:if test="@data-tocstart">
				<xsl:attribute name="data-tocstart">true</xsl:attribute>
			</xsl:if>
			<xsl:if test="@data-tocend">
				<xsl:attribute name="data-tocend">true</xsl:attribute>
			</xsl:if>
			<xsl:if test="@data-tocstyle">
				<xsl:attribute name="data-tocstyle">
					<xsl:value-of select="@data-tocstyle"/>
				</xsl:attribute>
			</xsl:if>
            <xsl:if test="@data-tochide">
				<xsl:attribute name="data-tochide">true</xsl:attribute>
			</xsl:if>
			<xsl:attribute name="class">
				<xsl:text>dd-item</xsl:text>
				<xsl:choose>
					<xsl:when test="$item-type = 'hierarchical'">
						<xsl:text> hierarchical</xsl:text>
					</xsl:when>
					<xsl:otherwise>
						<!-- for the full list, mark which elements are used elsewhere so that we can easily filter those -->
						<xsl:if test="/hierarchies/output_channel[@id = $output-channel-id]/items/structured//item[@data-ref = $data-ref]">
							<xsl:text> used</xsl:text>
						</xsl:if>
					</xsl:otherwise>
				</xsl:choose>
				<xsl:if test="$mode = 'acl-overview' and @id = 'cms_project-details' and contains($dd-content-class, 'nopermissions')">
					<xsl:text> alert alert-danger</xsl:text>
				</xsl:if>
			</xsl:attribute>
			
			<!--<xsl:text>(</xsl:text>
			<xsl:value-of select="@id"/>
			<xsl:text>)</xsl:text>-->
			
			<div class="dd-content">
				<xsl:attribute name="class">
					<xsl:value-of select="$dd-content-class"/>
				</xsl:attribute>

				<xsl:choose>
					<xsl:when test="$mode = 'acl-overview'">
						<xsl:choose>
							<xsl:when test="@aclrecord and @reset-inheritance">
								<span class="glyphicon glyphicon-check reset">
									<xsl:comment>.</xsl:comment>
								</span>
							</xsl:when>
							<xsl:when test="@aclrecord">
								<span class="glyphicon glyphicon-check">
									<xsl:comment>.</xsl:comment>
								</span>
							</xsl:when>
							<xsl:otherwise>
								<span class="glyphicon">&#160;</span>
							</xsl:otherwise>
						</xsl:choose>
					</xsl:when>
					<xsl:otherwise>
						<span class="dd-handle">||</span>
						<span class="dd-controls pre">
							<input type="checkbox" class="chk-select-item" id="chk-select-item__{@id}" name="chk-filter-linked-items"> </input>
						</span>
					</xsl:otherwise>
				</xsl:choose>
				<xsl:call-template name="render-linkname">
					<xsl:with-param name="item-node" select="."/>
					<xsl:with-param name="item-type" select="$item-type"/>
				</xsl:call-template>
				<xsl:if test="$view = 'sequential'">
					<span class="ai">
						<xsl:variable name="appears-in">
							<xsl:call-template name="generate-appear-in">
								<xsl:with-param name="data-ref" select="$data-ref"/>
							</xsl:call-template>
						</xsl:variable>

						<xsl:choose>
							<xsl:when test="string-length(normalize-space($appears-in)) = 0">
								<xsl:attribute name="class">ai not</xsl:attribute>
								<xsl:text>Not in use</xsl:text>
							</xsl:when>
							<xsl:otherwise>
								<xsl:text>Appears in: </xsl:text>
								<xsl:value-of select="$appears-in"/>
							</xsl:otherwise>
						</xsl:choose>
						<span>
							<xsl:text> (ref: </xsl:text>
							<xsl:value-of select="$data-ref"/>
							<xsl:if test="not(h1[@lang = $contentlang])">
								<xsl:text>, </xsl:text>
								<span class="headerissue">incorrect header</span>
							</xsl:if>
							<xsl:text>)</xsl:text>
						</span>
					</span>
				</xsl:if>
				<xsl:choose>
					<xsl:when test="$mode = 'acl-overview'">
						<xsl:choose>
							<xsl:when test="contains(web_page/path, '?pid=') and not(@id = 'cms_project-details')">
								<xsl:text></xsl:text>
							</xsl:when>
							<xsl:otherwise>
								<span class="dd-controls post">
									<a tabindex="0" class="btn btn-xs" onclick="od(this, '{@id}')" data-toggle="popover" data-trigger="focus" data-placement="right" data-showrestore="false">...</a>
								</span>								
							</xsl:otherwise>
						</xsl:choose>
					</xsl:when>
					<xsl:otherwise>
						<span class="dd-controls post">
							<a tabindex="0" class="btn btn-xs" onclick="od(this, '{@id}')" data-toggle="popover" data-trigger="focus" data-placement="right" data-showrestore="false">...</a>
						</span>
					</xsl:otherwise>
				</xsl:choose>



				<span class="data">
					<span class="path">
						<xsl:value-of select="web_page/path"/>
					</span>
					<span class="reportingrequirements-{$contentlang}">
						<xsl:variable name="reportingrequirements">
							<xsl:value-of select="@*[contains(local-name(), concat('data-reportingrequirements-', $contentlang))]"/>
						</xsl:variable>
						<xsl:choose>
							<xsl:when test="string-length(normalize-space($reportingrequirements)) > 0">
								<xsl:value-of select="$reportingrequirements"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:text> </xsl:text>
							</xsl:otherwise>
						</xsl:choose>
					</span>
					<span class="contentstatus-{$contentlang}">
						<xsl:variable name="contentstatus">
							<xsl:value-of select="@*[contains(local-name(), concat('data-contentstatus-', $contentlang))]"/>
						</xsl:variable>
						<xsl:choose>
							<xsl:when test="string-length(normalize-space($contentstatus)) > 0">
								<xsl:value-of select="$contentstatus"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:text> </xsl:text>
							</xsl:otherwise>
						</xsl:choose>
					</span>
				</span>
			</div>

			<xsl:if test="$item-type = 'hierarchical'">
				<!-- Render the sub items or inject a dummy <ul/> element that you can use to create hierarchical nested lists -->
				<xsl:choose>
					<xsl:when test="sub_items">
						<xsl:apply-templates select="sub_items">
							<xsl:with-param name="item-type" select="$item-type"/>
						</xsl:apply-templates>
					</xsl:when>
					<xsl:otherwise>
						<ul class="dd-list drag-placeholder">
							<xsl:comment>.</xsl:comment>
						</ul>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:if>

		</li>

	</xsl:template>

	<xsl:template match="sub_items">
		<xsl:param name="item-type"/>

		<ul class="dd-list">
			<xsl:if test="$mode = 'acl-overview' and ../@id = 'cms_content-editor'">
				<xsl:attribute name="class">
					<xsl:text>dd-list outputchannel</xsl:text>
				</xsl:attribute>
			</xsl:if>
			<xsl:apply-templates select="item">
				<xsl:with-param name="item-type" select="$item-type"/>
			</xsl:apply-templates>
		</ul>
	</xsl:template>


	<xsl:template name="render-linkname">
		<xsl:param name="item-node"/>
		<xsl:param name="item-type"/>

		<span class="linkname">
			<xsl:choose>
				<xsl:when test="$item-type = 'unstructured'">
					<xsl:variable name="item-id" select="$item-node/@id"/>
					<xsl:variable name="data-ref" select="$item-node/@data-ref"/>
					<xsl:attribute name="class">
						<xsl:text>linkname</xsl:text>
						<xsl:choose>
							<xsl:when test="linknames/linkname/@lang">
								<xsl:text> </xsl:text>
								<xsl:value-of select="linknames/linkname/@lang"/>
							</xsl:when>
						</xsl:choose>
					</xsl:attribute>
					<xsl:choose>
						<xsl:when test="/hierarchies/output_channel/items/structured//item[@data-ref = $data-ref]">
							<xsl:value-of select="/hierarchies/output_channel/items/structured//item[@data-ref = $data-ref]/web_page/linkname"/>
						</xsl:when>
						<xsl:when test="*[@lang]">
							<xsl:choose>
								<xsl:when test="h1[@lang = $contentlang]">
									<xsl:value-of select="h1[@lang = $contentlang]/text()"/>
								</xsl:when>
								<xsl:otherwise>
									<xsl:value-of select="*[@lang = $contentlang]/text()"/>
								</xsl:otherwise>
							</xsl:choose>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="h1/text()"/>
						</xsl:otherwise>
					</xsl:choose>

				</xsl:when>
				<xsl:otherwise>
					<xsl:attribute name="class">
						<xsl:text>linkname</xsl:text>
						<xsl:choose>
							<xsl:when test="linknames/linkname/@lang">
								<xsl:text> </xsl:text>
								<xsl:value-of select="linknames/linkname/@lang"/>
							</xsl:when>
						</xsl:choose>
					</xsl:attribute>
					
					<xsl:choose>
						<xsl:when test="$mode = 'acl-overview'">
							<xsl:copy-of select="web_page/linkname"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="web_page/linkname"/>
						</xsl:otherwise>
					</xsl:choose>
					
				</xsl:otherwise>
			</xsl:choose>

		</span>


	</xsl:template>

	<xsl:template name="generate-appear-in">
		<xsl:param name="data-ref"/>

		<xsl:choose>
			<xsl:when test="$filter-full-list = 'yes'">
				<xsl:for-each select="/hierarchies/output_channel[not(@id = $output-channel-id)]">
					<xsl:if test="count(.//item[@data-ref = $data-ref]) > 0">
						<xsl:value-of select="name"/>
					</xsl:if>
					<xsl:text> </xsl:text>
				</xsl:for-each>
			</xsl:when>
			<xsl:otherwise>
				<xsl:for-each select="/hierarchies/output_channel">
					<xsl:if test="count(.//item[@data-ref = $data-ref]) > 0">
						<xsl:value-of select="name"/>
					</xsl:if>
					<xsl:text> </xsl:text>
				</xsl:for-each>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>


</xsl:stylesheet>
